#if MAC_VERIFY && DEBUG
using System.Text.Json;
using UIKit;
namespace SchoolMessenger.Mobile;
public sealed partial class MessengerPage
{
    internal async Task MacVerifyLogin(string address,string password)
    {
        Preferences.Default.Set("update-confirmed-sequence",999999);
        api=new MessengerApi(address,true);await api.Login("admin",password,false);homeTab="notices";await Home();
    }
    internal MessengerApi MacVerifyApi=>api!;
    internal bool MacVerifyConnected=>desktopHub?.State==Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected;
    internal Task MacVerifyCompose()=>MacCompose();
    internal Task MacVerifyDetail(string id)=>OfficeDetail(id);
    internal async Task MacVerifyLogout(){var source=api!;await source.Logout();source.Dispose();api=null;Login();}
}
internal static class MacRuntimeChecks
{
    static readonly List<string> passed=[];
    static string Folder=>Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    static MessengerPage Page=>MessengerPage.Active!;
    static IEnumerable<View> Views(View view)
    {
        yield return view;IEnumerable<View> children=view switch{ScrollView s when s.Content is not null=>[s.Content],Border b when b.Content is not null=>[b.Content],Microsoft.Maui.Controls.Layout l=>l.Children.OfType<View>(),ContentView c when c.Content is not null=>[c.Content],_=>[]};
        foreach(var child in children)foreach(var descendant in Views(child))yield return descendant;
    }
    static void Check(bool value,string description){if(!value)throw new InvalidOperationException(description);passed.Add(description);}
    static async Task Wait(Func<bool> condition){for(var i=0;i<600;i++){if(condition())return;await Task.Delay(100);}throw new InvalidOperationException("Mac native UI timeout");}
    static void Capture(string name)
    {
        var window=UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>().SelectMany(s=>s.Windows).First(w=>w.IsKeyWindow);
        using var renderer=new UIGraphicsImageRenderer(window.Bounds.Size);using var image=renderer.CreateImage(context=>window.Layer.RenderInContext(context.CGContext));using var data=image.AsPNG();File.WriteAllBytes(Path.Combine(Folder,name),data!.ToArray());
    }
    public static async Task Run()
    {
        var report=Path.Combine(Folder,"mac-results.json");Directory.CreateDirectory(Folder);Console.WriteLine("MAC_REPORT_PATH:"+report);
        try
        {
            await Wait(()=>MessengerPage.Active?.Handler is not null);await Task.Delay(1200);
            var fixture=JsonDocument.Parse(Convert.FromBase64String(Environment.GetEnvironmentVariable("YY_MAC_FIXTURE_B64")!)).RootElement;
            Check(DeviceInfo.Platform==DevicePlatform.MacCatalyst,"Actual native Mac Catalyst process");
            Check(AppInfo.Current.Name=="교무톡"&&AppInfo.Current.VersionString=="1.0.0","Mac brand and pre-release version 1.0.0");
            Check(Page.Content is Grid grid&&grid.ColumnDefinitions.Count==2,"Native desktop sidebar and separate content column");Capture("mac-login.png");
            await Page.MacVerifyLogin(fixture.GetProperty("office").GetString()!.Replace("127.0.0.1","localhost"),fixture.GetProperty("password").GetString()!);
            Check(Page.MacVerifyApi.ProductCode=="teacher-macos"&&!Page.MacVerifyApi.Remember,"Mac product identity and session kept in memory for ad-hoc verification");
            await Wait(()=>Page.MacVerifyConnected);Check(true,"Authenticated desktop SignalR presence connects without mobile lease");Capture("mac-messages.png");
            var before=await Page.MacVerifyApi.Get("api/messages?box=received&offset=0");
            await Page.MacVerifyCompose();var compose=(ContentPage)Page.Navigation.NavigationStack.Last();var controls=Views(compose.Content).ToArray();
            controls.OfType<CheckBox>().First().IsChecked=true;controls.OfType<Entry>().Single().Text="Mac 네이티브 전송 검증";controls.OfType<Editor>().Single().Text="가상 학교 데이터로 Mac 메시지 작성과 수신 연결을 검사합니다.";Capture("mac-compose.png");
            var send=controls.OfType<Button>().Single(b=>b.Text=="메시지 보내기");((IButtonController)send).SendClicked();
            await Wait(()=>Page.Navigation.NavigationStack.Count==1);
            var rows=await Page.MacVerifyApi.Get("api/messages?box=sent&offset=0");var sent=rows.EnumerateArray().Single(r=>r.GetProperty("title").GetString()=="Mac 네이티브 전송 검증");
            Check(sent.GetProperty("recipientCount").GetInt32()==1,"Native composer sends through authenticated server with one selected recipient");
            await Page.MacVerifyDetail(sent.GetProperty("id").GetString()!);var detail=(ContentPage)Page.Navigation.NavigationStack.Last();
            Check(detail.Content is Grid details&&details.RowDefinitions.Count==2&&details.Children.OfType<Button>().Any(b=>b.Text=="답장"),"Reply remains in fixed footer outside the message scroll area");
            Capture("mac-message-detail.png");await Page.Navigation.PopAsync();await Page.MacVerifyLogout();
            Check(!Page.MacVerifyConnected,"Logout closes the desktop connection");
            try{_ =new MessengerApi("http://example.com",true);throw new InvalidOperationException("Unsafe HTTP accepted");}catch(InvalidOperationException e)when(e.Message.Contains("https://")){Check(true,"Non-local cleartext server address is rejected");}
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{ok=true,passed}));
        }
        catch(Exception e){await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{ok=false,passed,error=e.ToString()}));}
    }
}
#endif
