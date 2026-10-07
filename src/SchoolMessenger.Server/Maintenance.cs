using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SchoolMessenger.Server;

[Authorize]
public sealed class MessageHub(Presence presence, Store store) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var id = Context.UserIdentifier!;
        var identity = new LiveConnection(Context, id, Context.User!.FindFirstValue("version"));
        if (!identity.Valid(store)) { Context.Abort(); return; }
        presence.Live[Context.ConnectionId] = identity;
        presence.Connections[Context.ConnectionId] = id;
        await Groups.AddToGroupAsync(Context.ConnectionId, id);
        await Clients.All.SendAsync("PresenceChanged");
        await base.OnConnectedAsync();
    }
    public override async Task OnDisconnectedAsync(Exception? error)
    {
        presence.Connections.TryRemove(Context.ConnectionId, out _);
        presence.Live.TryRemove(Context.ConnectionId, out _);
        await Clients.All.SendAsync("PresenceChanged");
        await base.OnDisconnectedAsync(error);
    }
}
public sealed class Presence
{
    public ConcurrentDictionary<string, LiveConnection> Live { get; } = new();
    public ConcurrentDictionary<string, string> Connections { get; } = new();
    public bool Online(string userId) => Connections.Values.Contains(userId);
}
public sealed record LiveConnection(HubCallerContext Context, string UserId, string? Version)
{
    public bool Valid(Store store) => store.GetUser(UserId) is { Active: true } user && user.SessionVersion.ToString() == Version;
}
public sealed class Maintenance(Store store, IWebHostEnvironment environment, IConfiguration config,
    ILogger<Maintenance> logger) : BackgroundService
{
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public DateTimeOffset? LastCleanup { get; private set; }
    public DateTimeOffset? LastBackup { get; private set; }
    public string? LastError { get; private set; }
    public void Cleanup()
    {
        var expired = store.Query("""
            SELECT Id FROM Attachments WHERE DeletedAt IS NULL AND
            ((ExpiresAt IS NOT NULL AND ExpiresAt <= $now) OR (MessageId IS NULL AND UploadedAt <= $orphan AND NOT EXISTS(SELECT 1 FROM SubmissionFiles sf WHERE sf.AttachmentId=Attachments.Id)))
            """, r => r.GetString(0), ("$now", Now), ("$orphan", Now - 86_400_000));
        foreach (var id in expired)
        {
            File.Delete(Path.Combine(store.Files, id));
            store.Execute("UPDATE Attachments SET DeletedAt=$now WHERE Id=$id", ("$now", Now), ("$id", id));
        }
        // Recover files left behind by interrupted uploads before their database insert.
        var known = store.Query("SELECT Id FROM Attachments WHERE DeletedAt IS NULL", r => r.GetString(0)).ToHashSet();
        foreach (var path in Directory.EnumerateFiles(store.Files))
            if (!known.Contains(Path.GetFileName(path)) && File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-1))
                File.Delete(path);
        LastCleanup = DateTimeOffset.UtcNow;
    }
    public void Backup()
    {
        store.Backup();
        LastBackup = DateTimeOffset.UtcNow;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = environment.IsDevelopment() ? Math.Clamp(config.GetValue("School:CleanupSeconds", 3600), 1, 3600) : 3600;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Cleanup();
                if (LastBackup is null || LastBackup.Value.UtcDateTime.Date != DateTime.UtcNow.Date) Backup();
                LastError = null;
            }
            catch (Exception error)
            {
                LastError = "파일 정리 또는 백업 실패. 서버 기록을 확인하세요.";
                logger.LogError(error, "Maintenance failed");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
