using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using SchoolMessenger.AnnouncementServer;
using SchoolMessenger.Contracts;
using SchoolMessenger.Shared;
using static SchoolMessenger.AnnouncementServer.PortalSecurity;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 105L*1024*1024);
if (builder.Configuration["Portal:CertificateThumbprint"] is {Length:>0} thumbprint)
{
    using var certificates = new System.Security.Cryptography.X509Certificates.X509Store(System.Security.Cryptography.X509Certificates.StoreName.My,System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);
    certificates.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
    var certificate = certificates.Certificates.Find(System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,thumbprint,true).FirstOrDefault(c=>c.HasPrivateKey)
        ?? throw new InvalidOperationException("유효한 공지 서버 인증서와 개인키가 필요합니다.");
    builder.WebHost.ConfigureKestrel(o=>o.ConfigureHttpsDefaults(https=>https.ServerCertificate=certificate));
}
if (builder.Configuration["Portal:BridgeKey"]?.Length is not >= 32) throw new InvalidOperationException("Portal__BridgeKey에 별도 32자 이상 서비스 키를 설정하세요.");
var store = new PortalStore(builder.Configuration);
var dataPath = builder.Services.AddDataProtection().SetApplicationName("YyAnnouncements").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(store.Root,"keys")));
if (OperatingSystem.IsWindows()) dataPath.ProtectKeysWithDpapi();
builder.Services.AddSingleton(store);
builder.Services.AddSingleton<PasswordHasher<Account>>(); builder.Services.Configure<PasswordHasherOptions>(o=>o.IterationCount=220_000);
builder.Services.AddHostedService<PortalMaintenance>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o=>o.MultipartBodyLengthLimit=105L*1024*1024);
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="YyAnnouncements.Csrf";o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>
{
    o.Cookie.Name="YyAnnouncements.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
    o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
    o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
    o.Events.OnValidatePrincipal=async c=>{var person=store.Person(c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);if(person is not {Active:true} || person.PasswordHash is null || person.Version.ToString()!=c.Principal!.FindFirstValue("version") || person.Role=="teacher"&&!VerifiedTeacher(store,person)){c.RejectPrincipal();await c.HttpContext.SignOutAsync();}};
});
builder.Services.AddAuthorization(o=>o.AddPolicy("Admin",p=>p.RequireRole("admin")));
builder.Services.AddRateLimiter(o=>
{
    o.RejectionStatusCode=429;
    o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.User.Identity?.IsAuthenticated==true?"user:"+Id(c):"ip:"+c.Connection.RemoteIpAddress,_=>new FixedWindowRateLimiterOptions{PermitLimit=600,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddPolicy("login",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddPolicy("upload",_=>RateLimitPartition.GetConcurrencyLimiter("uploads",_=>new ConcurrencyLimiterOptions{PermitLimit=4,QueueLimit=0}));
});
var app=builder.Build();
var hasher=app.Services.GetRequiredService<PasswordHasher<Account>>();
if(store.Query("SELECT Id FROM People WHERE Role='admin'",r=>r.GetString(0)).Count==0)
{
    var password=builder.Configuration["Portal:AdminPassword"];if(!Password(password))throw new InvalidOperationException("첫 실행: Portal__AdminPassword에 12~128자 비밀번호를 지정하세요.");
    var administrator=new Account(Guid.NewGuid().ToString("N"),"admin","학교 공지 관리자","admin",null,null,false,true,1,null,0,0);
    store.Execute("INSERT INTO People(Id,Username,Name,Role,PasswordHash) VALUES($id,'admin',$name,'admin',$hash)",("$id",administrator.Id),("$name",administrator.Name),("$hash",hasher.HashPassword(administrator,password!)));
}
File.WriteAllText(Path.Combine(store.Root,".initialized"),"initialized");
var hosts=(builder.Configuration["Portal:AllowedHosts"]??"localhost,127.0.0.1").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
if(!app.Environment.IsDevelopment() && hosts.All(h=>h is "localhost" or "127.0.0.1")) app.Logger.LogWarning("외부 공개 전 Portal__AllowedHosts에 학교 도메인을 설정하세요.");
app.UseExceptionHandler(error=>error.Run(async c=>{c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new{error="서버 처리 실패. 같은 요청 번호로 다시 시도하거나 관리자에게 문의하세요."});}));
app.Use(async(c,next)=>
{
    if(!hosts.Contains(c.Request.Host.Host,StringComparer.OrdinalIgnoreCase)){c.Response.StatusCode=400;return;}
    if(!c.Request.IsHttps && c.Connection.RemoteIpAddress is {} ip && !System.Net.IPAddress.IsLoopback(ip)){c.Response.StatusCode=400;return;}
    if(c.Request.Headers.TryGetValue("Origin",out var origins) && (origins.Count!=1 || origins[0]!=$"{c.Request.Scheme}://{c.Request.Host}")){c.Response.StatusCode=403;return;}
    if(c.Request.Path.StartsWithSegments("/api")||c.Request.Path.StartsWithSegments("/bridge"))
    {c.Response.Headers.CacheControl="no-store"; if(!c.Request.Path.Value!.EndsWith("/attachments",StringComparison.Ordinal)){var limit=c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();if(limit is{IsReadOnly:false})limit.MaxRequestBodySize=c.Request.Path=="/bridge/timetable"?8_388_608:2_097_152;}}
    c.Response.Headers["X-Content-Type-Options"]="nosniff";c.Response.Headers["X-Frame-Options"]="DENY";c.Response.Headers["Referrer-Policy"]="no-referrer";
    c.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    c.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
    if(c.Request.IsHttps)c.Response.Headers["Strict-Transport-Security"]="max-age=31536000";
    await next();
});
app.UseDefaultFiles();app.UseStaticFiles();
if (app.Environment.IsDevelopment() && Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot")) });
app.UseRouting();app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();
app.Use(async(c,next)=>
{
    if(c.Request.Path.StartsWithSegments("/bridge")&&!Bridge(c,app.Configuration)){c.Response.StatusCode=401;return;}
    if(c.Request.Path.StartsWithSegments("/api")&&c.Request.Method is "POST" or "PATCH" or "DELETE")
        try{await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}catch(AntiforgeryValidationException){await Error("요청 인증 만료. 다시 로그인하세요.").ExecuteAsync(c);return;}
    await next();
});
app.MapGet("/health",()=>Results.Ok(new{status="ok",service="announcements"}));
app.MapGet("/api/session",(HttpContext c,IAntiforgery csrf)=>new PortalSession(app.Configuration["Portal:Name"]??"여양고 학교 소식",csrf.GetAndStoreTokens(c).RequestToken!,c.User.Identity?.IsAuthenticated==true?store.Public(store.Person(Id(c))!):null));
app.MapPost("/api/login",async Task<IResult>(Login request,HttpContext c)=>
{
    if(!Username(request.Username)||request.Password?.Length is not (>=1 and <=128))return Error("계정과 비밀번호를 확인하세요.",401);
    var person=store.Username(request.Username!);
    if(person is not{Active:true,PasswordHash:not null}||person.LockedUntil>Now)return Error("계정과 비밀번호를 확인하세요.",401);
    var verified=hasher.VerifyHashedPassword(person,person.PasswordHash,request.Password!);
    if(verified==PasswordVerificationResult.Failed){store.Execute("UPDATE People SET FailedAttempts=FailedAttempts+1,LockedUntil=CASE WHEN FailedAttempts+1>=5 THEN $until ELSE 0 END WHERE Id=$id",("$until",Now+900_000),("$id",person.Id));return Error("계정과 비밀번호를 확인하세요.",401);}
    if(person.Role=="teacher"&&!VerifiedTeacher(store,person))return Error("학교 교직원 연결 확인을 기다리거나 관리자에게 문의하세요.",403);
    if(verified==PasswordVerificationResult.SuccessRehashNeeded)store.Execute("UPDATE People SET PasswordHash=$hash WHERE Id=$id AND PasswordHash=$old",("$hash",hasher.HashPassword(person,request.Password!)),("$id",person.Id),("$old",person.PasswordHash));
    store.Execute("UPDATE People SET FailedAttempts=0,LockedUntil=0 WHERE Id=$id",("$id",person.Id));
    await c.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,person.Id),new Claim(ClaimTypes.Name,person.Name),new Claim(ClaimTypes.Role,person.Role),new Claim("version",person.Version.ToString())},CookieAuthenticationDefaults.AuthenticationScheme)),new AuthenticationProperties{IsPersistent=request.RememberLogin,ExpiresUtc=DateTimeOffset.UtcNow.Add(request.RememberLogin?TimeSpan.FromDays(30):TimeSpan.FromHours(8))});
    return Results.Ok(store.Public(person));
}).RequireRateLimiting("login");
app.MapPost("/api/register",(InviteRegistration request)=>PortalAdministration.Register(store,hasher,request)).RequireRateLimiting("login");
var api=app.MapGroup("/api").RequireAuthorization();
api.MapPost("/logout",async(HttpContext c)=>{store.Execute("UPDATE People SET Version=Version+1 WHERE Id=$id",("$id",Id(c)));await c.SignOutAsync();return Results.Ok();});
PortalAdministration.Map(app.MapGroup("/api/admin").RequireAuthorization("Admin"),store);
PortalNotices.Map(api,app.MapGroup("/bridge"),store,app.Configuration,app.Environment);
PortalTimetables.Map(api,app.MapGroup("/bridge"),store);
app.Services.GetServices<IHostedService>().OfType<PortalMaintenance>().Single().Cleanup();
app.Run();

record Login(string? Username,string? Password,bool RememberLogin=false);
