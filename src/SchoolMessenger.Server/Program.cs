using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using SchoolMessenger.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 210L * 1024 * 1024);
if (builder.Configuration["School:CertificateThumbprint"] is { Length: > 0 } thumbprint)
{
    using var certificates = new System.Security.Cryptography.X509Certificates.X509Store(
        System.Security.Cryptography.X509Certificates.StoreName.My, System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
    certificates.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
    var certificate = certificates.Certificates.Find(System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint, thumbprint, true)
        .FirstOrDefault(c => c.HasPrivateKey) ?? throw new InvalidOperationException("유효한 서버 인증서와 개인키를 찾을 수 없습니다.");
    builder.WebHost.ConfigureKestrel(o => o.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate));
}
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 105L * 1024 * 1024);
builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<Presence>();
builder.Services.AddSingleton<RemoteSessions>();
builder.Services.AddHostedService(s => s.GetRequiredService<RemoteSessions>());
builder.Services.AddSingleton<Maintenance>();
builder.Services.AddHostedService(s => s.GetRequiredService<Maintenance>());
builder.Services.AddSingleton<PasswordHasher<User>>();
builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 220_000);
builder.Services.AddHostedService<SubmissionReminders>();
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 4096);
builder.Services.AddSignalR().AddHubOptions<RemoteHub>(o => { o.MaximumReceiveMessageSize = 4096; o.MaximumParallelInvocationsPerClient = 1; });
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "SchoolMessenger.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        var user = c.HttpContext.RequestServices.GetRequiredService<Store>().GetUser(c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user is not { Active: true } || user.SessionVersion.ToString() != c.Principal!.FindFirstValue("version"))
        {
            c.RejectPrincipal();
            await c.HttpContext.SignOutAsync();
        }
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireRole("Admin")));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c =>
        RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("upload", _ => RateLimitPartition.GetConcurrencyLimiter("uploads", _ => new ConcurrencyLimiterOptions { PermitLimit = 4, QueueLimit = 0 }));
    o.AddPolicy("zip", _ => RateLimitPartition.GetConcurrencyLimiter("zip", _ => new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 0 }));
});

var app = builder.Build();
var store = app.Services.GetRequiredService<Store>();
var hasher = app.Services.GetRequiredService<PasswordHasher<User>>();
if (store.Users().Count == 0)
{
    var password = builder.Configuration["School:AdminPassword"];
    if (!ValidPassword(password)) throw new InvalidOperationException("첫 실행: School__AdminPassword에 12~128자 관리자 비밀번호를 지정하세요.");
    var admin = new User(Guid.NewGuid().ToString("N"), "admin", "정보담당", "관리", "", true, true, true, 1, 0, 0);
    InsertUser(store, admin, hasher.HashPassword(admin, password!));
}
File.WriteAllText(Path.Combine(store.Root, ".initialized"), "initialized");
var networks = (builder.Configuration["School:AllowedNetworks"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(System.Net.IPNetwork.Parse).ToArray();
// Never accept credentials over plain HTTP from another computer, including Development.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !(context.Request.Method == "POST" && context.Request.Path == "/api/attachments"))
    {
        var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = context.Request.Path.StartsWithSegments("/api/surveys") ? 524_288 : 131_072;
    }
    if (!context.Request.IsHttps && context.Connection.RemoteIpAddress is { } ip && !System.Net.IPAddress.IsLoopback(ip))
    { context.Response.StatusCode = 400; await context.Response.WriteAsync("교내 접속에는 HTTPS가 필요합니다."); return; }
    if (context.Connection.RemoteIpAddress is { } remote && !System.Net.IPAddress.IsLoopback(remote))
    {
        if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        if (!networks.Any(n => n.Contains(remote))) { context.Response.StatusCode = 403; return; }
    }
    if (context.Request.Headers.TryGetValue("Origin", out var origins) &&
        (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hub") || context.Request.Path.StartsWithSegments("/remote")))
    {
        var expected = new Uri($"{context.Request.Scheme}://{context.Request.Host}");
        if (origins.Count != 1 || !Uri.TryCreate(origins[0], UriKind.Absolute, out var origin) ||
            origin.GetLeftPart(UriPartial.Authority) != expected.GetLeftPart(UriPartial.Authority) || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.UserInfo.Length != 0)
        { context.Response.StatusCode = 403; return; }
    }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (context.Request.IsHttps) context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    await next();
});
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "서버 처리 실패. 저장 공간과 서버 기록을 확인하세요. 전송 재시도 시 같은 요청 번호를 사용하세요." });
}));
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is "POST" or "PATCH" or "DELETE")
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "요청 인증 만료. 다시 로그인하세요." }); return; }
    }
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/session", (HttpContext c, IAntiforgery antiforgery) => Results.Ok(new
{
    schoolName = builder.Configuration["School:Name"],
    csrfToken = antiforgery.GetAndStoreTokens(c).RequestToken,
    user = c.User.Identity?.IsAuthenticated == true ? PublicUser(store.GetUser(UserId(c))!) : null
}));
app.MapPost("/api/login", async Task<IResult> (Login request, HttpContext c) =>
{
    if (request.Username is null || request.Password is null || request.Password.Length > 128) return Error("계정과 비밀번호를 확인하세요.", 401);
    var user = store.FindUser(request.Username.Trim());
    if (user is null && store.FindRegistration(request.Username.Trim()) is { } registration)
    {
        var pendingUser = new User(registration.Id, registration.Username, registration.Name, registration.Department, registration.PasswordHash, false, false, false, 1, 0, 0);
        if (hasher.VerifyHashedPassword(pendingUser, registration.PasswordHash, request.Password) != PasswordVerificationResult.Failed)
            return Error(registration.Status == "pending" ? "관리자 승인 대기 중입니다. 승인 후 로그인할 수 있습니다." : "계정 신청이 반려되었습니다. 정보를 확인하고 다시 신청하세요.", 403);
    }
    if (user is not { Active: true } || user.LockedUntil > Maintenance.Now) return Error("계정과 비밀번호를 확인하세요. 반복 실패 시 15분 후 재시도하세요.", 401);
    var verified = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (verified == PasswordVerificationResult.Failed)
    {
        store.Execute("UPDATE Users SET FailedAttempts=FailedAttempts+1, LockedUntil=CASE WHEN FailedAttempts+1>=5 THEN $until ELSE 0 END WHERE Id=$id",
            ("$until", Maintenance.Now + 900_000), ("$id", user.Id));
        return Error("계정과 비밀번호를 확인하세요. 반복 실패 시 15분 후 재시도하세요.", 401);
    }
    if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        store.Execute("UPDATE Users SET PasswordHash=$hash WHERE Id=$id AND PasswordHash=$old", ("$hash", hasher.HashPassword(user, request.Password)), ("$id", user.Id), ("$old", user.PasswordHash));
    store.Execute("UPDATE Users SET FailedAttempts=0,LockedUntil=0 WHERE Id=$id", ("$id", user.Id));
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.Name), new("version", user.SessionVersion.ToString()) };
    if (user.IsAdmin) claims.Add(new(ClaimTypes.Role, "Admin"));
    await c.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = request.RememberLogin, ExpiresUtc = DateTimeOffset.UtcNow.Add(request.RememberLogin ? TimeSpan.FromDays(30) : TimeSpan.FromHours(8)) });
    return Results.Ok(PublicUser(user));
}).RequireRateLimiting("login");
app.MapPost("/api/register", IResult (RegistrationRequest request) =>
{
    if (!ValidProfile(request.Username, request.Name, request.Department) || !ValidPassword(request.Password)) return Error("아이디는 영문·숫자·._- 3~32자, 이름·부서는 1~50자, 비밀번호는 12~128자입니다.");
    var username = request.Username!.Trim();
    if (store.FindUser(username) is not null) return Error("이미 사용 중인 아이디입니다.", 409);
    var user = new User(Guid.NewGuid().ToString("N"), username, request.Name!.Trim(), request.Department!.Trim(), "", false, false, false, 1, 0, 0);
    var saved = store.Execute("""
        INSERT INTO Registrations(Id,Username,Name,Department,PasswordHash,Status,CreatedAt) VALUES($id,$username,$name,$department,$hash,'pending',$now)
        ON CONFLICT(Username) DO UPDATE SET Id=excluded.Id,Name=excluded.Name,Department=excluded.Department,PasswordHash=excluded.PasswordHash,Status='pending',CreatedAt=excluded.CreatedAt WHERE Registrations.Status='rejected'
        """, ("$id", user.Id), ("$username", username), ("$name", user.Name), ("$department", user.Department), ("$hash", hasher.HashPassword(user, request.Password!)), ("$now", Maintenance.Now));
    return saved == 0 ? Error("이미 신청한 아이디입니다. 관리자 승인 후 로그인하세요.", 409) : Results.Ok(new { status = "pending", message = "계정 신청 완료. 관리자 승인 후 로그인할 수 있습니다." });
}).RequireRateLimiting("login");
app.MapPost("/api/logout", async (HttpContext c, RemoteSessions sessions) =>
{
    store.Execute("UPDATE Users SET SessionVersion=SessionVersion+1 WHERE Id=$id", ("$id", UserId(c)));
    string[] ids; lock (sessions.Gate) ids = sessions.Sessions.Values.Where(s => s.Offer.HostId == UserId(c) || s.Offer.ViewerId == UserId(c)).Select(s => s.Offer.Id).ToArray();
    foreach (var id in ids) await sessions.End(id, "상대방이 로그아웃했습니다.");
    await c.SignOutAsync(); return Results.Ok();
}).RequireAuthorization();
app.MapPost("/api/password", async Task<IResult> (PasswordChange request, HttpContext c) =>
{
    var user = store.GetUser(UserId(c))!;
    if (!ValidPassword(request.NewPassword) || string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128)
        return Error("새 비밀번호는 12~128자입니다.");
    if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        return Error("현재 비밀번호가 일치하지 않습니다.");
    store.Execute("UPDATE Users SET PasswordHash=$hash,SessionVersion=SessionVersion+1 WHERE Id=$id", ("$hash", hasher.HashPassword(user, request.NewPassword!)), ("$id", user.Id));
    await c.SignOutAsync();
    return Results.Ok();
}).RequireAuthorization();
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/users", (Presence presence) => store.Users().Where(u => u.Active).Select(u => new
{ u.Id, u.Name, u.Department, online = presence.Online(u.Id) }));

api.MapPost("/attachments", async Task<IResult> (HttpContext c) =>
{
    if (!c.Request.HasFormContentType) return Error("파일을 선택하세요.");
    var form = await c.Request.ReadFormAsync(c.RequestAborted);
    if (form.Files.Count != 1) return Error("파일은 한 번에 하나씩 업로드하세요.");
    var file = form.Files[0];
    var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
    var extension = Path.GetExtension(name).ToLowerInvariant();
    var allowed = new[] { ".hwp", ".hwpx", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".png", ".jpg", ".jpeg", ".zip" };
    if (name.Length is < 1 or > 200 || name.Any(char.IsControl) || !allowed.Contains(extension)) return Error("지원하지 않는 파일 형식입니다.");
    if (file.Length is <= 0 or > 104_857_600) return Error("파일당 100MB까지 첨부할 수 있습니다.");
    var owner = UserId(c);
    var staged = store.Query("SELECT COALESCE(SUM(Size),0) FROM Attachments a WHERE OwnerId=$id AND MessageId IS NULL AND DeletedAt IS NULL AND NOT EXISTS(SELECT 1 FROM SubmissionFiles sf WHERE sf.AttachmentId=a.Id)",
        r => r.GetInt64(0), ("$id", owner))[0];
    if (staged + file.Length > 524_288_000) return Error("미전송 첨부 용량이 500MB를 넘습니다. 첨부를 정리하거나 메시지를 보내세요.", 413);
    if (new DriveInfo(Path.GetPathRoot(store.Root)!).AvailableFreeSpace < file.Length + 524_288_000)
        return Error("서버 저장 공간이 부족합니다. 관리자에게 문의하세요.", 507);
    var id = Guid.NewGuid().ToString("N");
    var path = Path.Combine(store.Files, id);
    try
    {
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await file.CopyToAsync(output, c.RequestAborted);
        if (!FileSignatureMatches(path, extension)) { File.Delete(path); return Error("파일 내용과 확장자가 일치하지 않습니다."); }
        if (!app.Environment.IsDevelopment())
        {
            var scanner = builder.Configuration["School:ScannerPath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
            if (!File.Exists(scanner)) { File.Delete(path); return Error("첨부파일 검사기를 사용할 수 없습니다. 관리자에게 문의하세요.", 503); }
            var info = new ProcessStartInfo(scanner) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[] { "-Scan", "-ScanType", "3", "-File", path, "-DisableRemediation" }) info.ArgumentList.Add(argument);
            using var scan = Process.Start(info)!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try { await scan.WaitForExitAsync(timeout.Token); }
            catch { if (!scan.HasExited) scan.Kill(true); throw; }
            if (scan.ExitCode != 0 || !File.Exists(path)) { File.Delete(path); return Error("파일 검사 실패 또는 위험 파일입니다.", 422); }
        }
        var inserted = store.Execute("""
            INSERT INTO Attachments(Id,OwnerId,Name,Size,UploadedAt)
            SELECT $id,$owner,$name,$size,$now WHERE
            (SELECT COALESCE(SUM(Size),0) FROM Attachments a WHERE OwnerId=$owner AND MessageId IS NULL AND DeletedAt IS NULL AND NOT EXISTS(SELECT 1 FROM SubmissionFiles sf WHERE sf.AttachmentId=a.Id))+$size <= 524288000
            """, ("$id", id), ("$owner", owner), ("$name", name), ("$size", file.Length), ("$now", Maintenance.Now));
        if (inserted == 0) { File.Delete(path); return Error("미전송 첨부 용량이 500MB를 넘습니다.", 413); }
        return Results.Ok(new { id, name, size = file.Length });
    }
    catch { File.Delete(path); throw; }
}).RequireRateLimiting("upload");
api.MapDelete("/attachments/{id}", (string id, HttpContext c) =>
{
    var changed = store.Execute("UPDATE Attachments SET DeletedAt=$now WHERE Id=$id AND OwnerId=$owner AND MessageId IS NULL AND DeletedAt IS NULL AND NOT EXISTS(SELECT 1 FROM SubmissionFiles sf WHERE sf.AttachmentId=Attachments.Id)",
        ("$now", Maintenance.Now), ("$id", id), ("$owner", UserId(c)));
    if (changed == 0) return Results.NotFound();
    File.Delete(Path.Combine(store.Files, id));
    return Results.Ok();
});

api.MapPost("/messages", (SendMessage request, HttpContext c, IHubContext<MessageHub> hub) => SendStoredMessage(request, c, hub));
api.MapPost("/chats/{roomId}/messages", (string roomId, ChatSend request, HttpContext c, IHubContext<MessageHub> hub) =>
    SendStoredMessage(new SendMessage(request.ClientId, "대화", request.Body, [], request.AttachmentIds, false, null), c, hub, roomId));
async Task<IResult> SendStoredMessage(SendMessage request, HttpContext c, IHubContext<MessageHub> hub, string? roomId = null)
{
    if (request.SubmissionDeadline is not null && roomId is not null) return Error("제출 요청은 쪽지에서 작성하세요.");
    if (request.SubmissionDeadline is { } rawDeadline)
    {
        if (rawDeadline < 0 || rawDeadline > 253_402_267_199_999) return Error("제출 마감 날짜를 확인하세요.");
        var day = TaskFeatures.KoreanDate(rawDeadline);
        request = request with { SubmissionDeadline = new DateTimeOffset(day.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.FromHours(9)).ToUnixTimeMilliseconds() };
    }
    if (roomId is not null)
    {
        var room = store.Query("SELECT r.Name FROM ChatRooms r JOIN ChatMembers cm ON cm.RoomId=r.Id WHERE r.Id=$room AND cm.UserId=$user", r => r.GetString(0), ("$room", roomId), ("$user", UserId(c))).FirstOrDefault();
        if (room is null) return Results.NotFound();
        var members = store.Query("SELECT cm.UserId FROM ChatMembers cm JOIN Users u ON u.Id=cm.UserId WHERE RoomId=$room AND u.Active=1", r => r.GetString(0), ("$room", roomId));
        request = request with { Title = room, RecipientIds = members.ToArray(), All = false, Department = null };
        if (string.IsNullOrWhiteSpace(request.Body) && request.AttachmentIds is { Length: > 0 }) request = request with { Body = "첨부파일" };
    }
    if (!Guid.TryParse(request.ClientId, out _) || string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 ||
        string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 20_000 || request.RecipientIds is null || request.AttachmentIds is null ||
        request.RecipientIds.Length > 1000 || request.AttachmentIds.Length > 10 || (request.Department?.Length ?? 0) > 50)
        return Error("제목·본문·수신자를 확인하세요. 첨부는 최대 10개입니다.");
    var sender = store.GetUser(UserId(c))!;
    // Preserve fingerprints for ordinary messages queued by an older desktop version.
    var fingerprintJson = roomId is null ? request.SubmissionDeadline is null ? JsonSerializer.Serialize(new
    { request.Title, request.Body, request.All, request.Department, recipients = request.RecipientIds.Distinct().Order(), attachments = request.AttachmentIds.Distinct().Order() }) : JsonSerializer.Serialize(new
    { request.Title, request.Body, request.All, request.Department, request.SubmissionDeadline, recipients = request.RecipientIds.Distinct().Order(), attachments = request.AttachmentIds.Distinct().Order() }) : JsonSerializer.Serialize(new
    { request.Title, request.Body, request.All, request.Department, roomId, recipients = request.RecipientIds.Distinct().Order(), attachments = request.AttachmentIds.Distinct().Order() });
    var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintJson)));
    string id;
    List<string> recipients;
    using (var connection = store.Open())
    using (var transaction = connection.BeginTransaction())
    {
        using (var existing = Store.Command(connection, "SELECT Id,Fingerprint FROM Messages WHERE SenderId=$sender AND ClientId=$client", transaction,
            ("$sender", sender.Id), ("$client", request.ClientId)))
        using (var reader = existing.ExecuteReader())
            if (reader.Read()) return reader.GetString(1) == fingerprint ? Results.Ok(new { id = reader.GetString(0), duplicate = true }) : Error("같은 요청 번호로 다른 메시지를 보낼 수 없습니다.", 409);
        var active = store.Users().Where(u => u.Active).ToList();
        if (request.SubmissionDeadline is { } deadline && (deadline <= Maintenance.Now || TaskFeatures.KoreanDate(deadline) > TaskFeatures.KoreanDate(Maintenance.Now).AddDays(366))) return Error("제출 마감은 오늘 이후 1년 이내로 지정하세요.");
        recipients = request.RecipientIds.Distinct().ToList();
        if (recipients.Any(id => active.All(u => u.Id != id))) return Error("존재하지 않거나 비활성화된 수신자입니다.");
        if (!string.IsNullOrEmpty(request.Department)) recipients.AddRange(active.Where(u => u.Department == request.Department).Select(u => u.Id));
        if (request.All) recipients.AddRange(active.Select(u => u.Id));
        recipients = recipients.Distinct().ToList();
        if (recipients.Count == 0) return Error("받는 사람을 선택하세요.");
        if (request.SubmissionDeadline is not null && recipients.All(u => u == sender.Id)) return Error("본인 외의 제출 대상자를 선택하세요.");
        if ((request.All || (roomId is null && active.Count > 1 && recipients.Count == active.Count)) && !sender.CanBroadcast && !sender.IsAdmin)
            return Error("전체 발송 권한이 없습니다.", 403);
        long total = 0;
        foreach (var fileId in request.AttachmentIds.Distinct())
        {
            using var command = Store.Command(connection, "SELECT Size FROM Attachments a WHERE Id=$id AND OwnerId=$owner AND MessageId IS NULL AND DeletedAt IS NULL AND UploadedAt>$cutoff AND NOT EXISTS(SELECT 1 FROM SubmissionFiles sf WHERE sf.AttachmentId=a.Id)", transaction,
                ("$id", fileId), ("$owner", sender.Id), ("$cutoff", Maintenance.Now - 86_400_000));
            var size = command.ExecuteScalar();
            if (size is null || !Guid.TryParseExact(fileId, "N", out _) || !File.Exists(Path.Combine(store.Files, fileId))) return Error("사용할 수 없는 첨부파일입니다.");
            total += Convert.ToInt64(size);
        }
        if (total > 209_715_200) return Error("메시지당 첨부 합계는 200MB입니다.", 413);
        id = Guid.NewGuid().ToString("N");
        var now = Maintenance.Now;
        using (var insert = Store.Command(connection, "INSERT INTO Messages VALUES($id,$sender,$client,$fingerprint,$title,$body,$now)", transaction,
            ("$id", id), ("$sender", sender.Id), ("$client", request.ClientId), ("$fingerprint", fingerprint), ("$title", request.Title.Trim()), ("$body", request.Body), ("$now", now))) insert.ExecuteNonQuery();
        foreach (var recipient in recipients)
        {
            using var insert = Store.Command(connection, "INSERT INTO Recipients(MessageId,UserId) VALUES($message,$user)", transaction, ("$message", id), ("$user", recipient));
            insert.ExecuteNonQuery();
        }
        var seconds = app.Environment.IsDevelopment() ? Math.Max(1, builder.Configuration.GetValue("School:RetentionSeconds", 2_592_000)) : 2_592_000;
        foreach (var fileId in request.AttachmentIds.Distinct())
        {
            using var update = Store.Command(connection, "UPDATE Attachments SET MessageId=$message,ExpiresAt=$expires WHERE Id=$id", transaction,
                ("$message", id), ("$expires", now + seconds * 1000L), ("$id", fileId));
            update.ExecuteNonQuery();
        }
        if (roomId is not null)
        {
            using var entry = Store.Command(connection, "INSERT INTO ChatEntries(RoomId,MessageId) VALUES($room,$message)", transaction, ("$room", roomId), ("$message", id)); entry.ExecuteNonQuery();
        }
        TaskFeatures.Extract(connection, transaction, recipients.Where(u => u != sender.Id), id, request.Body!, now);
        if (request.SubmissionDeadline is { } due)
        {
            using var taskRequest = Store.Command(connection, "INSERT INTO SubmissionRequests(MessageId,Deadline) VALUES($id,$due)", transaction, ("$id", id), ("$due", due)); taskRequest.ExecuteNonQuery();
            foreach (var recipient in recipients.Where(u => u != sender.Id))
            { using var target = Store.Command(connection, "INSERT INTO SubmissionTargets(RequestId,UserId) VALUES($id,$user)", transaction, ("$id", id), ("$user", recipient)); target.ExecuteNonQuery(); }
        }
        transaction.Commit();
    }
    // Database commit is the source of truth; notification failure must not undo delivery.
    try { await hub.Clients.Groups(recipients).SendAsync(roomId is null ? "NewMessage" : "ChatChanged", roomId ?? id); }
    catch (Exception error) { app.Logger.LogWarning(error, "Notification failed for stored message {MessageId}", id); }
    return Results.Ok(new { id, duplicate = false });
}
api.MapGet("/messages", (HttpContext c, string? box, string? q, string? person, long? from, long? to, int? offset) =>
{
    if ((q?.Length ?? 0) > 200) return Error("검색어는 200자 이내입니다.");
    var sent = box == "sent";
    var sql = """
        SELECT m.Id,m.Title,m.Body,m.CreatedAt,u.Name,
          (SELECT ReadAt FROM Recipients WHERE MessageId=m.Id AND UserId=$user),
          (SELECT COUNT(*) FROM Recipients WHERE MessageId=m.Id),
          (SELECT COUNT(*) FROM Recipients WHERE MessageId=m.Id AND ReadAt IS NOT NULL),
          (SELECT COUNT(*) FROM Attachments WHERE MessageId=m.Id),m.SenderId
        FROM Messages m JOIN Users u ON u.Id=m.SenderId
        WHERE
        """ + (box == "conversation" ? " (m.SenderId=$user OR EXISTS(SELECT 1 FROM Recipients r WHERE r.MessageId=m.Id AND r.UserId=$user)) " : sent ? " m.SenderId=$user " : " EXISTS(SELECT 1 FROM Recipients r WHERE r.MessageId=m.Id AND r.UserId=$user) ") +
        " AND NOT EXISTS(SELECT 1 FROM ChatEntries ce WHERE ce.MessageId=m.Id) AND ($person='' OR m.SenderId=$person OR EXISTS(SELECT 1 FROM Recipients r WHERE r.MessageId=m.Id AND r.UserId=$person)) " +
        (box == "unread" ? " AND EXISTS(SELECT 1 FROM Recipients r WHERE r.MessageId=m.Id AND r.UserId=$user AND r.ReadAt IS NULL) " : "") +
        " AND ($q='' OR instr(m.Title,$q)>0 OR instr(m.Body,$q)>0 OR instr(u.Name,$q)>0) AND m.CreatedAt>=$from AND m.CreatedAt<=$to ORDER BY m.CreatedAt DESC,m.Id DESC LIMIT 100 OFFSET $offset";
    var rows = store.Query(sql, r => new
    {
        id = r.GetString(0), title = r.GetString(1), preview = r.GetString(2)[..Math.Min(100, r.GetString(2).Length)],
        createdAt = r.GetInt64(3), senderName = r.GetString(4), readAt = r.IsDBNull(5) ? (long?)null : r.GetInt64(5),
        recipientCount = r.GetInt32(6), readCount = r.GetInt32(7), attachmentCount = r.GetInt32(8), senderId = r.GetString(9)
    }, ("$user", UserId(c)), ("$q", q ?? ""), ("$person", person ?? ""), ("$from", from ?? 0), ("$to", to ?? long.MaxValue), ("$offset", Math.Clamp(offset ?? 0, 0, 1_000_000)));
    return Results.Ok(rows);
});
api.MapGet("/messages/{id}", (string id, HttpContext c) =>
{
    var messages = store.Query("""
        SELECT m.Id,m.SenderId,u.Name,m.Title,m.Body,m.CreatedAt FROM Messages m JOIN Users u ON u.Id=m.SenderId
        WHERE m.Id=$id AND (m.SenderId=$user OR EXISTS(SELECT 1 FROM Recipients WHERE MessageId=m.Id AND UserId=$user))
        """, r => new { id = r.GetString(0), senderId = r.GetString(1), senderName = r.GetString(2), title = r.GetString(3), body = r.GetString(4), createdAt = r.GetInt64(5) }, ("$id", id), ("$user", UserId(c)));
    if (messages.Count == 0) return Results.NotFound();
    var attachments = store.Query("SELECT Id,Name,Size,ExpiresAt,DeletedAt FROM Attachments WHERE MessageId=$id", r => new
    { id = r.GetString(0), name = r.GetString(1), size = r.GetInt64(2), expiresAt = r.GetInt64(3), expired = r.GetInt64(3) <= Maintenance.Now || !r.IsDBNull(4) }, ("$id", id));
    var recipients = store.Query("SELECT u.Id,u.Name,u.Department,r.ReadAt FROM Recipients r JOIN Users u ON u.Id=r.UserId WHERE r.MessageId=$id ORDER BY u.Name", r => new
    { id = r.GetString(0), name = r.GetString(1), department = r.GetString(2), readAt = r.IsDBNull(3) ? (long?)null : r.GetInt64(3) }, ("$id", id));
    var submissionRequestId = store.Query("SELECT MessageId FROM SubmissionRequests WHERE MessageId=$id UNION SELECT RequestId FROM SubmissionReminders WHERE MessageId=$id", r => r.GetString(0), ("$id", id)).FirstOrDefault();
    return Results.Ok(new { message = messages[0], attachments, recipients, submissionRequest = submissionRequestId is not null, submissionRequestId });
});
api.MapPost("/messages/{id}/read", async Task<IResult> (string id, HttpContext c, IHubContext<MessageHub> hub) =>
{
    var changed = store.Execute("UPDATE Recipients SET ReadAt=COALESCE(ReadAt,$now) WHERE MessageId=$id AND UserId=$user", ("$now", Maintenance.Now), ("$id", id), ("$user", UserId(c)));
    if (changed == 0) return Results.NotFound();
    var sender = store.Query("SELECT SenderId FROM Messages WHERE Id=$id", r => r.GetString(0), ("$id", id))[0];
    try { await hub.Clients.Group(sender).SendAsync("ReadChanged", id); } catch { /* Next refresh reads the durable receipt. */ }
    return Results.Ok();
});
api.MapGet("/attachments/{id}/download", (string id, HttpContext c) =>
{
    var rows = store.Query("""
        SELECT a.Name,a.ExpiresAt,a.DeletedAt FROM Attachments a
        WHERE a.Id=$id AND (
          EXISTS(SELECT 1 FROM Messages m WHERE m.Id=a.MessageId AND (m.SenderId=$user OR EXISTS(SELECT 1 FROM Recipients WHERE MessageId=m.Id AND UserId=$user))) OR
          EXISTS(SELECT 1 FROM SubmissionFiles sf JOIN Submissions s ON s.Id=sf.SubmissionId JOIN Messages m ON m.Id=s.RequestId WHERE sf.AttachmentId=a.Id AND (s.UserId=$user OR m.SenderId=$user)))
        """, r => new { name = r.GetString(0), expires = r.GetInt64(1), deleted = !r.IsDBNull(2) }, ("$id", id), ("$user", UserId(c)));
    if (rows.Count == 0) return Results.NotFound();
    if (rows[0].expires <= Maintenance.Now || rows[0].deleted) return Error("첨부파일 보관 기간이 만료되었습니다.", 410);
    if (!Guid.TryParseExact(id, "N", out _)) return Results.NotFound();
    var path = Path.Combine(store.Files, id);
    if (!File.Exists(path)) return Error("첨부파일이 없습니다. 발신자에게 재전송을 요청하세요.", 410);
    return Results.File(path, "application/octet-stream", rows[0].name, enableRangeProcessing: false);
});

var adminApi = app.MapGroup("/api/admin").RequireAuthorization("Admin");
CollaborationFeatures.Map(api, store);
TaskFeatures.Map(api, store);
SubmissionFeatures.Map(api, store, app.Configuration, app.Environment);
adminApi.MapGet("/registrations", () => store.PendingRegistrations().Select(r => new { r.Id, r.Username, r.Name, r.Department, r.CreatedAt }));
adminApi.MapPost("/registrations/{id}/approve", (string id) => DecideRegistration(id, true));
adminApi.MapPost("/registrations/{id}/reject", (string id) => DecideRegistration(id, false));
IResult DecideRegistration(string id, bool approve)
{
    using var connection = store.Open(); using var transaction = connection.BeginTransaction();
    Registration registration;
    using (var command = Store.Command(connection, "SELECT * FROM Registrations WHERE Id=$id AND Status='pending'", transaction, ("$id", id)))
    using (var reader = command.ExecuteReader())
    { if (!reader.Read()) return Error("처리할 승인 대기 신청이 없습니다.", 409); registration = Store.ReadRegistration(reader); }
    if (approve)
    {
        using var create = Store.Command(connection, "INSERT INTO Users(Id,Username,Name,Department,PasswordHash,IsAdmin,CanBroadcast) VALUES($id,$username,$name,$department,$hash,0,0)", transaction,
            ("$id", registration.Id), ("$username", registration.Username), ("$name", registration.Name), ("$department", registration.Department), ("$hash", registration.PasswordHash));
        try { create.ExecuteNonQuery(); } catch (SqliteException error) when (error.SqliteErrorCode == 19) { return Error("동일한 계정이 이미 등록되었습니다. 신청 정보를 확인하세요.", 409); }
    }
    using var decide = Store.Command(connection, "UPDATE Registrations SET Status=$status WHERE Id=$id", transaction, ("$status", approve ? "approved" : "rejected"), ("$id", id));
    decide.ExecuteNonQuery(); transaction.Commit(); return Results.Ok();
}
adminApi.MapGet("/users", () => store.Users().Select(PublicUser));
adminApi.MapPost("/users", IResult (CreateUser request) =>
{
    if (!ValidProfile(request.Username, request.Name, request.Department) || !ValidPassword(request.Password)) return Error("아이디는 영문·숫자·._- 3~32자, 이름·부서는 1~50자, 비밀번호는 12~128자입니다.");
    if (store.FindUser(request.Username!) is not null) return Error("이미 사용 중인 아이디입니다.", 409);
    var user = new User(Guid.NewGuid().ToString("N"), request.Username!, request.Name!.Trim(), request.Department!.Trim(), "", request.IsAdmin, request.CanBroadcast, true, 1, 0, 0);
    try { InsertUser(store, user, hasher.HashPassword(user, request.Password!)); }
    catch (SqliteException e) when (e.SqliteErrorCode == 19) { return Error("이미 사용 중인 아이디입니다.", 409); }
    return Results.Ok(PublicUser(user));
});
adminApi.MapPatch("/users/{id}", IResult (string id, UpdateUser request, HttpContext c) =>
{
    var user = store.GetUser(id);
    if (user is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50 || string.IsNullOrWhiteSpace(request.Department) || request.Department.Length > 50 ||
        (!string.IsNullOrEmpty(request.Password) && !ValidPassword(request.Password))) return Error("이름·부서·비밀번호 형식을 확인하세요.");
    if (id == UserId(c) && (!request.Active || !request.IsAdmin)) return Error("자신의 관리자 권한을 해제하거나 계정을 비활성화할 수 없습니다.");
    store.Execute("UPDATE Users SET Name=$name,Department=$department,Active=$active,IsAdmin=$admin,CanBroadcast=$broadcast,PasswordHash=$hash,SessionVersion=SessionVersion+1,FailedAttempts=0,LockedUntil=0 WHERE Id=$id",
        ("$name", request.Name.Trim()), ("$department", request.Department.Trim()), ("$active", request.Active), ("$admin", request.IsAdmin), ("$broadcast", request.CanBroadcast),
        ("$hash", string.IsNullOrEmpty(request.Password) ? user.PasswordHash : hasher.HashPassword(user, request.Password)), ("$id", id));
    return Results.Ok();
});
adminApi.MapGet("/status", (Maintenance maintenance, Presence presence) =>
{
    var drive = new DriveInfo(Path.GetPathRoot(store.Root)!);
    return Results.Ok(new { online = presence.Connections.Values.Distinct().Count(), users = store.Users().Count(u => u.Active),
        filesBytes = Directory.EnumerateFiles(store.Files).Sum(f => new FileInfo(f).Length), freeBytes = drive.AvailableFreeSpace,
        maintenance.LastCleanup, maintenance.LastBackup, maintenance.LastError,
        attachmentBackup = false, retentionDays = 30, messageRetention = "자동 삭제 없음: 학교 보관 정책 확정 필요" });
});
adminApi.MapPost("/maintenance", (Maintenance maintenance) => { maintenance.Cleanup(); maintenance.Backup(); return Results.Ok(); });
app.MapHub<MessageHub>("/hub", options => options.CloseOnAuthenticationExpiration = true);
app.MapHub<RemoteHub>("/remote", options => { options.CloseOnAuthenticationExpiration = true; options.ApplicationMaxBufferSize = 4096; options.TransportMaxBufferSize = 4096; });
// Cleanup before accepting connections also handles expiry while the server was offline.
app.Services.GetRequiredService<Maintenance>().Cleanup();
app.Run();

static string UserId(HttpContext c) => c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
static object PublicUser(User u) => new { u.Id, u.Username, u.Name, u.Department, u.IsAdmin, u.CanBroadcast, u.Active };
static IResult Error(string message, int status = 400) => Results.Json(new { error = message }, statusCode: status);
static bool ValidPassword(string? password) => password is { Length: >= 12 and <= 128 } && !string.IsNullOrWhiteSpace(password);
static bool ValidProfile(string? username, string? name, string? department) => username is not null && Regex.IsMatch(username, "^[A-Za-z0-9._-]{3,32}$") &&
    !string.IsNullOrWhiteSpace(name) && name.Length <= 50 && !string.IsNullOrWhiteSpace(department) && department.Length <= 50;
static void InsertUser(Store store, User u, string hash) => store.Execute("INSERT INTO Users(Id,Username,Name,Department,PasswordHash,IsAdmin,CanBroadcast) VALUES($id,$username,$name,$department,$hash,$admin,$broadcast)",
    ("$id", u.Id), ("$username", u.Username), ("$name", u.Name), ("$department", u.Department), ("$hash", hash), ("$admin", u.IsAdmin), ("$broadcast", u.CanBroadcast));
static bool FileSignatureMatches(string path, string extension)
{
    Span<byte> bytes = stackalloc byte[8];
    using var stream = File.OpenRead(path);
    var length = stream.Read(bytes);
    if (extension is ".docx" or ".xlsx" or ".pptx" or ".hwpx" or ".zip") return length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4b && bytes[2] == 0x03 && bytes[3] == 0x04;
    if (extension is ".doc" or ".xls" or ".ppt" or ".hwp") return length == 8 && bytes.SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 });
    if (extension == ".pdf") return length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8);
    if (extension == ".png") return length == 8 && bytes.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    if (extension is ".jpg" or ".jpeg") return length >= 3 && bytes[..3].SequenceEqual(new byte[] { 255, 216, 255 });
    return extension is ".txt" or ".csv";
}
record Login(string? Username, string? Password, bool RememberLogin = false);
record RegistrationRequest(string? Username, string? Name, string? Department, string? Password);
record PasswordChange(string? CurrentPassword, string? NewPassword);
record SendMessage(string? ClientId, string? Title, string? Body, string[]? RecipientIds, string[]? AttachmentIds, bool All, string? Department, long? SubmissionDeadline = null);
record CreateUser(string? Username, string? Name, string? Department, string? Password, bool IsAdmin, bool CanBroadcast);
record UpdateUser(string? Name, string? Department, string? Password, bool IsAdmin, bool CanBroadcast, bool Active);
