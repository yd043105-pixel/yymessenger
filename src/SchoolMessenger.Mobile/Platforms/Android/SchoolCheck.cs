using System.Text.Json;
using Android.Content;
using AndroidX.Core.App;
using AndroidX.Work;

namespace SchoolMessenger.Mobile;

// Once a day, at the time the school set (default 18:00, spread over the next 30 minutes so a school's phones do not
// all call at once), the app asks its own school server for news and for a move notice and shows a notification.
// No push service or central server is involved: the phone only talks to the school's server.
public sealed class SchoolCheckWorker(Context context,WorkerParameters parameters):Worker(context,parameters)
{
    public override Result DoWork()
    {
        try{SchoolCheck.Run(ApplicationContext).GetAwaiter().GetResult();}
        catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException or KeyNotFoundException){}
        SchoolCheck.Schedule(ApplicationContext);
        return Result.InvokeSuccess();
    }
}

public static class SchoolCheck
{
    const string Work="school-daily-check",Channel="school-news";
    public static void Schedule(Context context)
    {
        var time=TimeOnly.TryParseExact(Preferences.Default.Get("check-time","18:00"),"HH:mm",out var value)?value:new TimeOnly(18,0);
        var now=DateTime.Now;var next=now.Date+time.ToTimeSpan();if(next<=now)next=next.AddDays(1);
        var delay=next-now+TimeSpan.FromSeconds(Random.Shared.Next(0,1800));
        var request=(OneTimeWorkRequest)new OneTimeWorkRequest.Builder(typeof(SchoolCheckWorker)).SetInitialDelay((long)delay.TotalMilliseconds,Java.Util.Concurrent.TimeUnit.Milliseconds!)
            .SetConstraints(new Constraints.Builder().SetRequiredNetworkType(NetworkType.Connected!).Build()).Build();
        WorkManager.GetInstance(context).EnqueueUniqueWork(Work,ExistingWorkPolicy.Replace!,request);
    }
    public static async Task Run(Context context)
    {
        var mode=Preferences.Default.Get("mode",0);var office=mode==1;var address=Preferences.Default.Get("address-"+mode,"");
        if(address.Length==0)return;
        if(await MessengerPage.FollowMove(address,office) is{} moved)
        {
            Notify(context,2,"학교 서버 주소가 바뀌었습니다","앱이 새 주소로 연결합니다. 앱을 열어 확인하세요.");address=moved;
        }
        if(office)return;
        // Only a remembered sign-in can check news in the background; otherwise the app checks when it is opened.
        using var api=new MessengerApi(address,false);await api.Restore();if(!api.Remember)return;await api.Refresh();
        if(api.Session.GetProperty("user").ValueKind!=JsonValueKind.Object)return;
        var notices=(await api.Get("api/announcements?offset=0")).EnumerateArray().ToArray();if(notices.Length==0)return;
        var key="notified-"+address;var since=Preferences.Default.Get(key,0L);
        var fresh=notices.Where(n=>n.GetProperty("readAt").ValueKind==JsonValueKind.Null&&n.GetProperty("publishedAt").GetInt64()>since).ToArray();
        Preferences.Default.Set(key,Math.Max(since,notices.Max(n=>n.GetProperty("publishedAt").GetInt64())));
        if(fresh.Length>0)Notify(context,1,"새 학교 소식 "+fresh.Length+"건",fresh[0].GetProperty("title").GetString()??"");
    }
    static void Notify(Context context,int id,string title,string text)
    {
        var manager=NotificationManagerCompat.From(context);if(!manager.AreNotificationsEnabled())return;
        if(OperatingSystem.IsAndroidVersionAtLeast(26))
            ((Android.App.NotificationManager)context.GetSystemService(Context.NotificationService)!).CreateNotificationChannel(new Android.App.NotificationChannel(Channel,"학교 소식",Android.App.NotificationImportance.Default));
        var open=context.PackageManager!.GetLaunchIntentForPackage(context.PackageName!)!;
        var tap=Android.App.PendingIntent.GetActivity(context,id,open,Android.App.PendingIntentFlags.Immutable|Android.App.PendingIntentFlags.UpdateCurrent);
        var notification=new NotificationCompat.Builder(context,Channel).SetSmallIcon(context.ApplicationInfo!.Icon).SetContentTitle(title).SetContentText(text).SetContentIntent(tap).SetAutoCancel(true).Build();
        manager.Notify(id,notification);
    }
}
