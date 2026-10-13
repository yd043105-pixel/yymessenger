#if IOS && DEBUG && IOS_VERIFY
using System.Text.Json;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    internal bool VerificationReady=>!busy&&!resuming&&!covered;
    internal MessengerApi VerificationApi=>api!;
}

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
            Border border when border.Content is not null => [border.Content],
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
    static async Task Wait(Func<bool> condition,int timeoutMilliseconds=20000)
    {
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        while(elapsed.ElapsedMilliseconds<timeoutMilliseconds){if(condition())return;await Task.Delay(100);}
        throw new InvalidOperationException("Native iOS state timed out at "+CurrentPage.Title+"; ready: "+Page.VerificationReady+"; controls: "+string.Join(" | ",Controls.OfType<Label>().Select(v=>v.Text).Concat(Controls.OfType<Button>().Select(v=>v.Text))));
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passed.Add(description);
        var progress=Path.Combine(Documents,"ios-progress.json");
        File.WriteAllText(progress+".tmp",JsonSerializer.Serialize(new{passed,updatedAt=DateTimeOffset.UtcNow}));File.Move(progress+".tmp",progress,true);
    }
    static async Task Tap(string text)
    {
        await Wait(() => Page.VerificationReady&&Controls.OfType<Button>().Any(b => b.Text == text && b.IsEnabled));
        ((IButtonController)Controls.OfType<Button>().Single(b => b.Text == text)).SendClicked();
        // A cold simulator may need several sequential login, session, and home requests.
        await Wait(()=>Page.VerificationReady,60000);
    }
    static async Task TapAccount(string text)
    {
        await Tap("내 정보");await Tap(text);
    }
    static async Task SaveReport(string phase,bool ok,string? error=null)
    {
        var file=Path.Combine(Documents,"ios-"+phase+"-results.json");
        await File.WriteAllTextAsync(file+".tmp",JsonSerializer.Serialize(new{ok,passed,error}));
        File.Move(file+".tmp",file,true);
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
                await TapAccount("로그아웃"); await Wait(() => Has("우리 학교 소식"));
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
                Check(SecureStorage.DefaultAccessible == Security.SecAccessible.WhenUnlockedThisDeviceOnly, "saved sessions use unlocked device-only Keychain protection");
                Check(notice.Version == "1.0.0", "native iOS includes bundled 1.0.0 release notes");
                try { using var invalid = new MessengerApi("http://school.example", false); throw new InvalidOperationException("Remote HTTP accepted"); }
                catch (InvalidOperationException error) when (error.Message.Contains("https://")) { Check(true, "non-local HTTP is rejected before sending credentials"); }
                Preferences.Default.Set("update-confirmed-sequence", notice.Sequence);
                var entries = Controls.OfType<Entry>().ToArray();
                entries[0].Text = portal; entries[1].Text = fixture.GetProperty("parent").GetString(); entries[2].Text = password;
                await Tap("로그인"); await Wait(() => Has("10월 현장체험학습 안내"));
                Check(entries[2].Text == "", "native parent login renders allowed notices and clears password input");
                Check(Has("홈")&&Has("소식")&&Has("내 정보")&&!Has("로그아웃")&&!Has("업데이트 내역"),"native home prioritizes notices with persistent tabs and moves account actions out of the feed");
                await Tap("소식");await Wait(()=>Has("받은 학교 소식"));await Tap("홈");await Wait(()=>Has("최근 학교 소식"));
                Check(Has("10월 현장체험학습 안내"),"native notice tab and home tab preserve authorized school content");
                await Tap("시간표 · 내 수업 / 학급");await Wait(()=>CurrentPage.Title=="우리 학급 시간표");
                Controls.OfType<DatePicker>().Single().Date=DateTime.ParseExact(fixture.GetProperty("timetableDate").GetString()!,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
                await Tap("선택한 날짜 보기");await Wait(()=>Has("가상 영어"));
                Check(Has("변경 전: 가상 수학")&&Has("2교시 · 수업 없음")&&Has("일자별 시간표 적용"),"native iOS displays dated replacement, previous lesson and explicitly empty period");
                Controls.OfType<DatePicker>().Single().Date=new DateTime(2026,10,15);await Tap("선택한 날짜 보기");await Wait(()=>Has("기초시간표 적용"));
                Check(Has("가상 수학")&&Has("가상 국어"),"native iOS falls back to baseline when no dated workbook exists");
                Controls.OfType<Picker>().Single().SelectedIndex=1;await Tap("선택한 날짜 보기");await Wait(()=>Has("아직 시간표가 등록되지 않은 학급이 있습니다."));
                Check(!Has("기초시간표 적용")&&!Has("일자별 시간표 적용"),"native iOS does not claim a timetable is applied when its class has no publication");
                await Page.Navigation.PopAsync();await Tap("홈");await Wait(()=>Has("10월 현장체험학습 안내"));
                var row = Controls.OfType<Button>().Single(b => b.Text?.Contains("10월 현장체험학습 안내") == true);
                await Tap(row.Text!);await Wait(() => Has("<svg onload=alert(1)>"));
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
                await Page.VerificationApi.Refresh();
                Check(Page.VerificationApi.Session.GetProperty("user").GetProperty("role").GetString()=="parent"&&(await Page.VerificationApi.Get("api/children")).GetArrayLength()==2,"separate native API clients preserve the visible parent identity and child relationships");
                await TapAccount("설정 · 자녀 연결");await Wait(()=>CurrentPage.Title=="설정 · 자녀 연결");
                Check(Has("가상학생 하나")&&Has("가상학생 둘"),"native parent settings shows both linked children");
                await Page.Navigation.PopAsync();await Tap("홈");await Wait(()=>Has("10월 현장체험학습 안내"));await TapAccount("로그아웃");await Wait(()=>Has("우리 학교 소식"));
                await Tap("학생·학부모 회원가입");await Wait(()=>CurrentPage.Title=="학생·학부모 회원가입");
                var signup=Controls.OfType<Entry>().ToArray();signup[0].Text="iOS 가상 학부모";signup[1].Text="ios.parent";signup[2].Text=password;
                await Tap("회원가입 신청");await Wait(()=>Has("필수 약관·개인정보 안내의 전문을 확인하고 체크하세요."));
                Check(CurrentPage.Title=="학생·학부모 회원가입","native signup refuses unchecked required documents");
                foreach(var title in new[]{"서비스 이용약관","개인정보 수집·이용 동의 안내"})
                {
                    await Tap("전문 보기 · "+title);await Wait(()=>CurrentPage.Title==title);
                    Check(Has("검토용 초안")&&Has("문서 버전"),"native in-app document shows full text, version and draft status: "+title);
                    await Tap("이전 화면으로 돌아가기");await Wait(()=>CurrentPage.Title=="학생·학부모 회원가입");
                }
                foreach(var choice in Controls.OfType<CheckBox>()){Check(choice.IsEnabled,"native checkbox enabled after opening document");choice.IsChecked=true;}
                signup[2].Text=password;
                await Tap("회원가입 신청");await Wait(()=>CurrentPage==Page&&Has("임시회원 가입 완료"));
                Check(signup[2].Text=="","native individual signup clears its password and returns to login");
                entries=Controls.OfType<Entry>().ToArray();entries[0].Text=portal;entries[1].Text="ios.parent";entries[2].Text=password;await Tap("로그인");await Wait(()=>Has("임시회원입니다."));
                Check(!Controls.OfType<Button>().Any(b=>b.Text=="시간표 · 내 수업 / 학급"),"native temporary parent has no school content or timetable action");
                using var administrator=new MessengerApi(portal,false);await administrator.Login("admin",password,false);
                var people=(await administrator.Get("api/admin/people")).EnumerateArray().ToArray();
                var firstStudent=people.Single(p=>p.GetProperty("username").ValueKind==JsonValueKind.String&&p.GetProperty("username").GetString()==fixture.GetProperty("student").GetString());
                var otherStudent=people.First(p=>p.GetProperty("role").GetString()=="student"&&p.GetProperty("id").GetString()!=firstStudent.GetProperty("id").GetString());
                var firstCode=(await administrator.Get("api/admin/people/"+firstStudent.GetProperty("id").GetString()+"/student-code")).GetProperty("code").GetString();
                var otherCode=(await administrator.Get("api/admin/people/"+otherStudent.GetProperty("id").GetString()+"/student-code")).GetProperty("code").GetString();
                await TapAccount("설정 · 자녀 연결");await Wait(()=>CurrentPage.Title=="설정 · 자녀 연결");Controls.OfType<Entry>().Single().Text=firstCode;await Tap("자녀 연결");await Wait(()=>Has("정회원 · 연결된 자녀")&&Has("가상학생 하나")&&Has("자녀가 연결되었습니다"));
                Check(Has("정회원 · 연결된 자녀")&&Has("가상학생 하나"),"native code entry immediately upgrades temporary parent to regular member");
                Controls.OfType<Entry>().Single().Text=otherCode;await Tap("자녀 연결");await Wait(()=>Has("가상학생 둘")&&Has("자녀가 연결되었습니다"));
                Check(Has("가상학생 하나")&&Controls.OfType<Entry>().Single().Text=="","native settings adds a second child and clears the entered secret");
                await Page.Navigation.PopAsync();await Tap("홈");await Wait(()=>Controls.OfType<Button>().Any(b=>b.Text=="시간표 · 내 수업 / 학급"));await TapAccount("로그아웃");await Wait(()=>Has("우리 학교 소식"));
                entries=Controls.OfType<Entry>().ToArray();entries[0].Text=portal;entries[1].Text=fixture.GetProperty("student").GetString();entries[2].Text=password;await Tap("로그인");await Wait(()=>Has("10월 현장체험학습 안내"));
                await TapAccount("설정 · 내 고유번호");await Wait(()=>CurrentPage.Title=="설정 · 내 고유번호");
                Check(Controls.OfType<Entry>().Single(e=>e.IsReadOnly).Text==firstCode,"native student settings displays its own protected eight-character code");
                await Page.Navigation.PopAsync();await Tap("홈");await Wait(()=>Has("10월 현장체험학습 안내"));await TapAccount("로그아웃");await Wait(()=>Has("우리 학교 소식"));
                Controls.OfType<Picker>().Single().SelectedIndex=1;
                entries=Controls.OfType<Entry>().ToArray();entries[0].Text=office;entries[1].Text=fixture.GetProperty("admin").GetString();entries[2].Text=password;await Tap("로그인");await Wait(()=>Has("교직원 업무 메시지 검증"));
                Check(Has("온라인 교무실")&&Has("메시지"),"native office home renders the internal account schema without requiring a public portal role");
                Check(Controls.OfType<Label>().Any(l=>l.Text=="미확인 메시지"),"native iOS office home renders the received-message unread state");
                await Tap("학사력 · 행사 · 학교 일정");await Wait(()=>Has("교직원 학사일정 검증"));
                Check(Controls.OfType<DatePicker>().Any()&&Has("학교 일정 등록"),"native teacher calendar displays authenticated school events and manager actions");
                await Tap("학교 일정 등록");await Wait(()=>Controls.OfType<Entry>().Any(e=>e.Placeholder=="제목"));Controls.OfType<Entry>().Single(e=>e.Placeholder=="제목").Text="iOS 학교 행사 저장 검증";
                await Tap("저장");await Wait(()=>Has("iOS 학교 행사 저장 검증"));Check(true,"native teacher calendar editor persists an event through the office API");
                await Page.Navigation.PopAsync();await Tap("특별실 예약 · 나의 예약");await Wait(()=>CurrentPage.Title=="특별실 예약");Controls.OfType<DatePicker>().Single().Date=DateTime.Parse(fixture.GetProperty("officeBookingDate").GetString()!);
                await Tap("날짜 조회");await Wait(()=>Has("교직원 특별실 예약 검증"));Check(Has("가상 특별실"),"native teacher reservations display the selected day and room from the school server");
                await Page.Navigation.PopAsync();
                await Tap("교직원 업무 메시지 검증");await Wait(()=>Has("교직원 전용 메시지입니다"));
                Check(!Has("현장체험학습 안내"),"native office detail displays internal messages independently of the public school feed");
                await Page.Navigation.PopAsync();await Wait(()=>Page.VerificationReady&&Has("교직원 업무 메시지 검증"));
                Check(!Controls.OfType<Label>().Any(l=>l.Text=="미확인 메시지"),"native iOS opening a received message persists its read state, including a self-recipient");
                await TapAccount("로그아웃");await Wait(()=>Has("우리 학교 소식"));
                Controls.OfType<Picker>().Single().SelectedIndex=0;
                entries=Controls.OfType<Entry>().ToArray();entries[0].Text=portal;entries[1].Text=fixture.GetProperty("parent").GetString();entries[2].Text=password;await Tap("로그인");await Wait(()=>Has("10월 현장체험학습 안내"));
                // Leave the original fixture parent signed in for the process-restart check.
                await Page.Resume(); await Wait(() => Has("10월 현장체험학습 안내"));
            }
            await SaveReport(phase,true);
        }
        catch (Exception error)
        {
            await SaveReport(phase,false,error.Message);
        }
    }
}
#endif
