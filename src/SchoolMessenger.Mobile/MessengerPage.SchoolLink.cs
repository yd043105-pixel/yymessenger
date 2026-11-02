using System.Text.Json;

namespace SchoolMessenger.Mobile;

// 학교 연결 QR: the school's administrator program prints the server address, its certificate and the school.
// Scanning it in the app (or opening it from the phone camera) connects without typing an address. A school server
// that later moves tells the app its new address, which the app follows to the stated certificate.
public sealed partial class MessengerPage
{
    internal sealed record SchoolLinkInfo(string Server,string? Pin,string Name,string Address,bool Office);
    static Dictionary<string,string> LinkQuery(Uri uri)=>uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(p=>p.Split('=',2)).Where(p=>p.Length==2)
        .GroupBy(p=>p[0]).ToDictionary(g=>g.Key,g=>Uri.UnescapeDataString(g.First()[1]));
    static bool ValidPin(string? pin)=>pin is{Length:64}&&pin.All(Uri.IsHexDigit);
    internal static SchoolLinkInfo? ParseSchoolLink(string? link)
    {
        if(link is null||!Uri.TryCreate(link.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="schooltalk"||uri.Host!="school")return null;
        var query=LinkQuery(uri);
        if(!query.TryGetValue("server",out var server)||!Uri.TryCreate(server,UriKind.Absolute,out var serverUri)||serverUri.Scheme is not("https"or"http"))return null;
        var pin=query.GetValueOrDefault("pin");if(pin is not null&&!ValidPin(pin))return null;
        return new(server.TrimEnd('/'),pin?.ToUpperInvariant(),query.GetValueOrDefault("name",""),query.GetValueOrDefault("addr",""),query.GetValueOrDefault("service")=="office");
    }
    async Task<string?> UseSchoolLink(string link)
    {
        var info=ParseSchoolLink(link)??throw new InvalidOperationException("학교 연결 QR을 읽지 못했습니다. 학교에서 받은 QR을 다시 찍어 주세요.");
#if FAMILY_APP
        if(info.Office)throw new InvalidOperationException("교직원용 학교 연결 QR입니다. 교사 앱에서 찍어 주세요.");
#elif TEACHER_APP
        if(!info.Office)throw new InvalidOperationException("학생·학부모용 학교 연결 QR입니다. 학교톡 앱에서 찍어 주세요.");
#endif
        if(api is{Session.ValueKind:JsonValueKind.Object} source&&source.Session.GetProperty("user").ValueKind==JsonValueKind.Object)
            throw new InvalidOperationException("이미 로그인되어 있습니다. 로그아웃한 뒤 학교 연결 QR을 다시 찍어 주세요.");
        var owner=Navigation.NavigationStack.LastOrDefault()??this;
        var school=info.Name.Length>0?info.Name+(info.Address.Length>0?"\n"+info.Address:""):info.Server;
        if(!await owner.DisplayAlertAsync("학교 연결",school+"\n\n이 학교 서버에 연결할까요?","연결","취소"))return null;
        if(info.Pin is{} pin)
        {
            // A new QR is how a school recovers after replacing its server PC, so a changed certificate is accepted
            // only after a clear warning.
            if(CertificatePins.Pinned(info.Server) is{} known&&!string.Equals(known,pin,StringComparison.OrdinalIgnoreCase)
                &&!await owner.DisplayAlertAsync("연결 확인 코드 변경","이 주소의 연결 확인 코드가 예전과 다릅니다. 학교에서 새로 나눠 준 QR이 맞을 때만 계속하세요.","계속","취소"))return null;
            CertificatePins.Trust(info.Server,pin);
        }
        var mode=info.Office?1:0;
        Preferences.Default.Set("mode",mode);Preferences.Default.Set("address-"+mode,info.Server);
        if(info.Name.Length>0)Preferences.Default.Set("school-"+mode,info.Name+"\t"+info.Address);
        Login();
        return (info.Name.Length>0?info.Name:"학교")+" 서버에 연결했습니다. 아이디와 비밀번호로 로그인하거나 회원가입하세요.";
    }
    // A school server that moved answers only with its new address: follow it, to the certificate it states, and
    // remember it. Also keeps the school's daily check time. Returns the new address, or null when nothing moved.
    internal static async Task<string?> FollowMove(string address,bool office)
    {
        try
        {
            using var probe=new MessengerApi(address,office);var info=await probe.Get("server-info");
            if(!info.TryGetProperty("app",out var app))return null;
            if(app.TryGetProperty("checkTime",out var time)&&time.GetString() is{} checkTime&&TimeOnly.TryParseExact(checkTime,"HH:mm",out _))Preferences.Default.Set("check-time",checkTime);
            if(!app.TryGetProperty("moving",out var move)||move.ValueKind!=JsonValueKind.Object)return null;
            var next=move.GetProperty("address").GetString()??"";var pin=move.GetProperty("fingerprint").GetString()??"";
            if(!Uri.TryCreate(next,UriKind.Absolute,out var nextUri)||nextUri.Scheme!="https"||pin.Length>0&&!ValidPin(pin))return null;
            next=next.TrimEnd('/');if(pin.Length>0)CertificatePins.Trust(next,pin.ToUpperInvariant());
            Preferences.Default.Set("address-"+(office?1:0),next);return next;
        }
        catch(Exception e)when(e is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException or KeyNotFoundException){return null;}
    }
    // After a sign-in the daily check is (re)scheduled at the school's time; Android 13+ asks for notifications once.
    static async Task ScheduleChecks()
    {
#if ANDROID
        SchoolCheck.Schedule(Platform.AppContext);
        if(OperatingSystem.IsAndroidVersionAtLeast(33))await Permissions.RequestAsync<Permissions.PostNotifications>();
#else
        await Task.CompletedTask;
#endif
    }
#if ANDROID
    // In-app scanner for the school connection QR (and the guardian invite QR); the result goes through OpenLink.
    async Task ScanSchoolQr()
    {
        if(await Permissions.RequestAsync<Permissions.Camera>()!=PermissionStatus.Granted)throw new InvalidOperationException("카메라 권한을 허용해야 학교 QR을 찍을 수 있습니다.");
        var found=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader=new ZXing.Net.Maui.Controls.CameraBarcodeReaderView{Options=new ZXing.Net.Maui.BarcodeReaderOptions{Formats=ZXing.Net.Maui.BarcodeFormat.QrCode,AutoRotate=true,Multiple=false}};
        reader.BarcodesDetected+=(_,e)=>{if(e.Results.FirstOrDefault()?.Value is{} value&&value.StartsWith("schooltalk://",StringComparison.OrdinalIgnoreCase))found.TrySetResult(value);};
        var hint=Text("학교에서 받은 학교 연결 QR을 화면 가운데에 맞춰 주세요.",14);hint.TextColor=Colors.White;hint.BackgroundColor=Color.FromArgb("#99000000");hint.Padding=new Thickness(16,12);hint.VerticalOptions=LayoutOptions.End;
        var page=new ContentPage{Title="학교 QR 찍기",Content=new Grid{Children={reader,hint}}};page.Disappearing+=(_,_)=>found.TrySetResult(null);
        await Navigation.PushAsync(page);
        var link=await found.Task;reader.IsDetecting=false;
        if(Navigation.NavigationStack.LastOrDefault()==page)await Navigation.PopAsync();
        if(link is not null)OpenLink(link);
    }
#endif
}
