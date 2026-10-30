using System.Text.Json;

namespace SchoolMessenger.Mobile;

// 보호자 초대 QR: the student's phone shows a code, the guardian's camera opens 학교톡 from it with the school
// address, the certificate the student's phone trusts and a ten-minute invite, and the guardian signs up already
// linked to the child. The server treats holding the student's signed-in phone as the guardian check.
public sealed partial class MessengerPage
{
    static string? pendingLink;
    // Called by the platform when 학교톡 opens from a schooltalk://guardian link (MauiProgram lifecycle events).
    // Before the first screen has appeared the link waits; OnAppearing picks it up after any saved login is restored.
    public static void OpenLink(string? link)
    {
        if(link is null||!link.StartsWith("schooltalk://guardian",StringComparison.OrdinalIgnoreCase))return;
        pendingLink=link;
        // Run skips work while the page is busy (loading, signing in), so wait for it rather than drop the link.
        if(Active is{restored:true} page)MainThread.BeginInvokeOnMainThread(async()=>{for(var i=0;i<40&&(page.busy||page.resuming);i++)await Task.Delay(250);await page.Run(page.GuardianLinkShown);});
    }
    // The link can arrive while any screen is on top, so the outcome is shown in an alert rather than the home feedback line.
    async Task GuardianLinkShown()
    {
        var owner=Navigation.NavigationStack.LastOrDefault()??this;
        try{if(await GuardianLink() is{} done)await owner.DisplayAlertAsync("보호자 초대 QR",done,"확인");}
        catch(Exception e){await owner.DisplayAlertAsync("보호자 초대 QR",e is HttpRequestException?"학교 서버에 연결할 수 없습니다. 인터넷 연결을 확인하세요.":e.Message,"확인");}
    }
    async Task<string?> GuardianLink()
    {
        if(Interlocked.Exchange(ref pendingLink,null) is not{} link)return null;
#if TEACHER_APP
        await Task.CompletedTask;
        throw new InvalidOperationException("보호자 초대 QR 코드는 학교톡 앱에서 열어 주세요.");
#else
        var query=new Uri(link).Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(p=>p.Split('=',2)).Where(p=>p.Length==2).GroupBy(p=>p[0]).ToDictionary(g=>g.Key,g=>Uri.UnescapeDataString(g.First()[1]));
        if(!query.TryGetValue("server",out var server)||!query.TryGetValue("invite",out var invite)||!Uri.TryCreate(server,UriKind.Absolute,out var serverUri)||serverUri.Scheme is not("https"or"http"))
            throw new InvalidOperationException("보호자 초대 QR 코드를 읽지 못했습니다. 학생 앱에서 새 QR 코드를 만들어 주세요.");
        // The student's phone already confirmed this server's certificate; the guardian's phone trusts the same one.
        if(query.TryGetValue("pin",out var pin))
        {
            var known=CertificatePins.Pinned(server);
            if(known is null)CertificatePins.Trust(server,pin.ToUpperInvariant());
            else if(!string.Equals(known,pin,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(CertificateRefused);
        }
        if(api is{} source&&source.Session.ValueKind==JsonValueKind.Object&&source.Session.GetProperty("user").ValueKind==JsonValueKind.Object)
        {
            var sameSchool=source.Address.GetLeftPart(UriPartial.Authority)==serverUri.GetLeftPart(UriPartial.Authority);
            if(source.Session.GetProperty("user").GetProperty("role").GetString()!="parent"||!sameSchool)
                throw new InvalidOperationException("다른 계정으로 로그인되어 있습니다. 로그아웃한 뒤 QR 코드를 다시 찍어 주세요.");
            var child=Child(await source.Get("api/registration/guardian-invite/"+Uri.EscapeDataString(invite)));
            var owner=Navigation.NavigationStack.LastOrDefault()??this;
            if(!await owner.DisplayAlertAsync("자녀 연결",child+" 학생을 자녀로 연결할까요?","연결","취소"))return null;
            var linked=await source.Send("api/settings/children/invite",new MobileFamilyCode(invite));await source.Refresh();
            await Navigation.PopToRootAsync();await Home();return linked.GetProperty("message").GetString();
        }
        Preferences.Default.Set("mode",0);Preferences.Default.Set("address-0",server.TrimEnd('/'));Login();
        await ConfirmingCertificate(server,()=>Signup(server,invite));
        return null;
#endif
    }
    static string Child(JsonElement preview)=>preview.GetProperty("studentName").GetString()+"("+preview.GetProperty("className").GetString()+")";

    // Student side: a fresh QR code each time; the previous one stops working.
    View GuardianInviteSection(MessengerApi source,int version)
    {
        var section=new VerticalStackLayout{Spacing=12};
        section.Add(Text("보호자 초대 QR",20,true));
        section.Add(Text("보호자가 휴대폰 카메라로 찍으면 학교톡이 열리고, 가입과 동시에 자녀로 연결됩니다. 학교 확인을 기다리지 않아도 됩니다.",14));
        var code=new Image{WidthRequest=240,HeightRequest=240,HorizontalOptions=LayoutOptions.Center,IsVisible=false,AutomationId="guardian-qr"};
        var note=Text("",13);note.TextColor=Muted;
        var make=new Button{Text="보호자 초대 QR 만들기",BackgroundColor=Blue,TextColor=Colors.White,AutomationId="guardian-qr-make"};
        make.Clicked+=async(_,_)=>
        {
            make.IsEnabled=false;
            try
            {
                var address=source.Address.GetLeftPart(UriPartial.Authority);
                var invite=await source.Send("api/settings/guardian-invite",new MobileGuardianInvite(address,CertificatePins.Pinned(address)));if(!Current(source,version))return;
                var png=Convert.FromBase64String(invite.GetProperty("qr").GetString()!);code.Source=ImageSource.FromStream(()=>new MemoryStream(png));code.IsVisible=true;
                var until=DateTimeOffset.FromUnixTimeMilliseconds(invite.GetProperty("expiresAt").GetInt64()).ToLocalTime();
                note.Text=until.ToString("HH:mm")+"까지 한 번만 쓸 수 있습니다. 보호자에게 직접 보여 주고, 사진으로 찍어 다른 곳에 보내지 마세요.";make.Text="새 QR 만들기";
            }
            catch(Exception e){note.Text=e.Message;}
            finally{make.IsEnabled=true;}
        };
        section.Add(make);section.Add(code);section.Add(note);
        return section;
    }
}
