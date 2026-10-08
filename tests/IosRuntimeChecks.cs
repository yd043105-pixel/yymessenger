#if IOS && DEBUG && IOS_VERIFY
using System.Text.Json;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

// Compiled only into the explicitly requested simulator verification build.
internal static class IosRuntimeChecks
{
    static readonly List<string> passed = [];
    static string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    static IEnumerable<View> Views(View view)
    {
        yield return view;
        IEnumerable<View> children = view switch
        {
            ScrollView scroll when scroll.Content is not null => [scroll.Content],
            Microsoft.Maui.Controls.Layout layout => layout.Children.OfType<View>(),
            ContentView content when content.Content is not null => [content.Content],
            _ => []
        };
        foreach (var child in children) foreach (var descendant in Views(child)) yield return descendant;
    }
    static MessengerPage Page => MessengerPage.Active ?? throw new InvalidOperationException("Native page missing");
    static ContentPage CurrentPage => (ContentPage)Page.Navigation.NavigationStack.Last();
    static IEnumerable<View> Controls => Views(CurrentPage.Content);
    static bool Has(string text) => Controls.OfType<Label>().Any(v => v.Text?.Contains(text) == true) || Controls.OfType<Button>().Any(v => v.Text?.Contains(text) == true);
    static async Task Wait(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++) { if (condition()) return; await Task.Delay(100); }
        throw new InvalidOperationException("Native iOS state timed out");
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passed.Add(description);
    }
    static async Task Tap(string text)
    {
        await Wait(() => Controls.OfType<Button>().Any(b => b.Text == text && b.IsEnabled));
        ((IButtonController)Controls.OfType<Button>().Single(b => b.Text == text)).SendClicked();
    }
    public static async Task Run(string phase)
    {
        try
        {
            await Wait(() => Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is not null);
            var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Documents, "ios-test-fixture.json"))).RootElement;
            var portal = fixture.GetProperty("portal").GetString()!.Replace("127.0.0.1", "localhost");
            var office = fixture.GetProperty("office").GetString()!.Replace("127.0.0.1", "localhost");
            var password = fixture.GetProperty("password").GetString()!;
            var notice = SchoolMessenger.Shared.ReleaseNotice.Load("mobile");
            if (phase == "restore")
            {
                await Wait(() => Has("10월 현장체험학습 안내"));
                Check(true, "native iOS automatically restores Keychain session after process restart");
                using var copied = new MessengerApi(portal, false); await copied.Restore(); await copied.Refresh();
                await Tap("로그아웃"); await Wait(() => Has("우리 학교 소식"));
                using var cleared = new MessengerApi(portal, false); await cleared.Restore();
                Check(!cleared.Remember, "native logout removes secure saved session");
                await copied.Refresh();
                Check(copied.Session.GetProperty("user").ValueKind == JsonValueKind.Null, "logout revokes a previously copied persistent cookie");
            }
            else
            {
                await Wait(() => Has("우리 학교 소식"));
                await SecureStorage.Default.SetAsync("ios-verification-marker", "synthetic marker");
                App.PrepareIosStorage();
                Check(await SecureStorage.Default.GetAsync("ios-verification-marker") == "synthetic marker", "ordinary startup preserves this installation's Keychain data");
                Preferences.Default.Remove("ios-install-initialized"); App.PrepareIosStorage();
                Check(await SecureStorage.Default.GetAsync("ios-verification-marker") is null, "fresh installation clears inherited Keychain data");
                Check(SecureStorage.Default is SecureStorageImplementation storage && storage.DefaultAccessible == Security.SecAccessible.WhenUnlockedThisDeviceOnly, "saved sessions use unlocked device-only Keychain protection");
                Check(notice.Version == "1.0.0", "native iOS includes bundled 1.0.0 release notes");
                try { using var invalid = new MessengerApi("http://school.example", false); throw new InvalidOperationException("Remote HTTP accepted"); }
                catch (InvalidOperationException error) when (error.Message.Contains("https://")) { Check(true, "non-local HTTP is rejected before sending credentials"); }
                Preferences.Default.Set("update-confirmed-sequence", notice.Sequence);
                var entries = Controls.OfType<Entry>().ToArray();
                entries[0].Text = portal; entries[1].Text = fixture.GetProperty("parent").GetString(); entries[2].Text = password;
                await Tap("로그인"); await Wait(() => Has("10월 현장체험학습 안내"));
                Check(entries[2].Text == "", "native parent login renders allowed notices and clears password input");
                var row = Controls.OfType<Button>().Single(b => b.Text?.Contains("10월 현장체험학습 안내") == true);
                await Wait(() => row.IsEnabled);
                ((IButtonController)row).SendClicked(); await Wait(() => Has("<svg onload=alert(1)>"));
                Check(true, "native notice detail renders synthetic HTML literally and records receipt");
                Page.Cover(); Check(Page.Navigation.NavigationStack.OfType<ContentPage>().All(p => !p.Content.IsVisible), "background cover hides every native content page");
                await Page.Resume(); await Wait(() => Has("10월 현장체험학습 안내"));
                Check(CurrentPage.Content.IsVisible, "resume validates session before restoring native content");
                using var teacher = new MessengerApi(portal, false); await teacher.Login(fixture.GetProperty("teacher").GetString()!, password, false);
                var scopes = await teacher.Get("api/classes");
                var file = Path.Combine(FileSystem.CacheDirectory, "ios-synthetic-attachment.txt"); await File.WriteAllTextAsync(file, "Synthetic iOS announcement attachment");
                var upload = await teacher.Upload(new FileResult(file), true);
                var request = new AnnouncementRequest(Guid.NewGuid().ToString("N"), "iOS 공지 검증", "가상 iOS 공지입니다.", "both", [scopes.EnumerateArray().First().GetProperty("id").GetString()!], false, [upload.GetProperty("id").GetString()!]);
                var created = await teacher.Send("api/announcements", request);
                var published = await teacher.Get("api/announcements/" + created.GetProperty("id").GetString());
                Check(published.GetProperty("attachments").GetArrayLength() == 1, "iOS FileResult uploads and publishes a dedicated announcement attachment");
                using var internalApi = new MessengerApi(office, true); await internalApi.Login(fixture.GetProperty("admin").GetString()!, password, false);
                var messages = await internalApi.Get("api/messages?box=received&offset=0");
                Check(messages.EnumerateArray().Any(m => m.GetProperty("title").GetString() == "교직원 업무 메시지 검증"), "iOS office messages use an independent internal service and session");
                await internalApi.Logout();
            }
            await File.WriteAllTextAsync(Path.Combine(Documents, "ios-" + phase + "-results.json"), JsonSerializer.Serialize(new { ok = true, passed }));
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(Documents, "ios-" + phase + "-results.json"), JsonSerializer.Serialize(new { ok = false, passed, error = error.Message }));
        }
    }
}
#endif
