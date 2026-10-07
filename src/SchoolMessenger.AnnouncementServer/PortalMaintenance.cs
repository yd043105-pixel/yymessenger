using Microsoft.Data.Sqlite;

namespace SchoolMessenger.AnnouncementServer;

public sealed class PortalMaintenance(PortalStore store,IConfiguration config,IHostEnvironment environment,ILogger<PortalMaintenance> logger):BackgroundService
{
    string? backupDay;
    public void Cleanup()
    {
        var now=PortalSecurity.Now;
        var expired=store.Query("SELECT Id FROM Files WHERE DeletedAt IS NULL AND (ExpiresAt<=$now OR NoticeId IS NULL AND UploadedAt<=$cutoff)",r=>r.GetString(0),("$now",now),("$cutoff",now-86_400_000));
        foreach(var id in expired){File.Delete(Path.Combine(store.Files,id));store.Execute("UPDATE Files SET DeletedAt=$now WHERE Id=$id",("$now",now),("$id",id));}
        foreach(var path in Directory.EnumerateFiles(store.Files))
            if(File.GetLastWriteTimeUtc(path)<DateTime.UtcNow.AddDays(-1)&&store.Query("SELECT 1 FROM Files WHERE Id=$id",r=>r.GetInt32(0),("$id",Path.GetFileName(path))).Count==0)File.Delete(path);
        var day=DateTime.UtcNow.ToString("yyyyMMdd");if(day==backupDay)return;
        var folder=Path.GetFullPath(config["Portal:BackupDirectory"]??Path.Combine(store.Root,"backups"));Directory.CreateDirectory(folder);
        using(var source=store.Open())using(var target=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(folder,"announcements-"+day+".db")}.ToString())){target.Open();source.BackupDatabase(target);}
        foreach(var old in Directory.EnumerateFiles(folder,"announcements-*.db"))if(File.GetLastWriteTimeUtc(old)<DateTime.UtcNow.AddDays(-14))File.Delete(old);
        backupDay=day;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds=environment.IsDevelopment()?Math.Max(1,config.GetValue("Portal:CleanupSeconds",60)):60;
        while(!stoppingToken.IsCancellationRequested)
        {
            try{Cleanup();}catch(Exception error){logger.LogError(error,"Public announcement maintenance failed");}
            try{await Task.Delay(TimeSpan.FromSeconds(seconds),stoppingToken);}catch(OperationCanceledException){break;}
        }
    }
}
