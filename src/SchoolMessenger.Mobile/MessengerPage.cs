using System.Text.Json;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

public sealed class MessengerPage : ContentPage
{
    public static MessengerPage? Active{get;private set;}
    static readonly Color Navy=Color.FromArgb("#1c334f"),Blue=Color.FromArgb("#2864df");
    readonly VerticalStackLayout body=new(){Spacing=16,Padding=new Thickness(24,26)};
    readonly Label feedback=new(){TextColor=Color.FromArgb("#a53a38"),FontSize=13};
    MessengerApi? api;
    bool busy,covered,restored,resuming,preserveComposerOnResume;
    ContentPage? composer;
    bool updateNoticeOpen,updateNoticeOffered;
    int epoch,offset;
    string view="received",child="";
    bool unread;
    public MessengerPage()
    {
        MessengerApi.CleanupShares();Active=this;Title="여양고 · 학교 소식";BackgroundColor=Color.FromArgb("#f3f6fa");Content=new ScrollView{Content=body};Login();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if(!restored&&api is null)
        {
            restored=true;var mode=Preferences.Default.Get("mode",0);var address=Preferences.Default.Get("address-"+mode,"");
            if(address.Length>0)await Run(async()=>
            {
                var source=new MessengerApi(address,mode==1);await source.Restore();
                if(!source.Remember){source.Dispose();return;}
                api=source;source.Invalidated+=()=>{if(api==source)Invalidate();};await source.Refresh();
                if(source.Session.GetProperty("user").ValueKind!=JsonValueKind.Null)await Home();
            });
            return;
        }
        if(api is {Session.ValueKind:JsonValueKind.Object} source&&source.Session.GetProperty("user").ValueKind!=JsonValueKind.Null&&!covered)
            await Run(async()=>{await source.Refresh();if(source.Session.GetProperty("user").ValueKind!=JsonValueKind.Null)await Home();});
    }
    static Label Text(string value,int size=15,bool bold=false)=>new(){Text=value,TextColor=Navy,FontSize=size,FontAttributes=bold?FontAttributes.Bold:FontAttributes.None,LineBreakMode=LineBreakMode.WordWrap};
    void Header(string caption,string title){body.Clear();body.Add(Text(caption,12));body.Add(Text(title,28,true));body.Add(feedback);feedback.Text="";}
    Entry Input(string title,bool password=false,string? value=null){body.Add(Text(title,13,true));var input=new Entry{Placeholder=title,IsPassword=password,Text=value,TextColor=Navy,BackgroundColor=Colors.White};body.Add(input);return input;}
    void EnableActions(bool value){foreach(var button in body.Children.OfType<Button>())button.IsEnabled=value;}
    void Action(string label,Func<Task> action,bool primary=false){var b=new Button{IsEnabled=!busy&&!resuming,Text=label,BackgroundColor=primary?Blue:Colors.White,TextColor=primary?Colors.White:Navy,CornerRadius=10,Padding=new Thickness(16,12)};b.Clicked+=async(_,_)=>await Run(action);body.Add(b);}
    async Task Run(Func<Task> action){if(busy||resuming)return;busy=true;body.IsEnabled=false;EnableActions(false);feedback.Text="";try{await action();}catch(Exception e){feedback.Text=e is HttpRequestException?"학교 서버에 연결할 수 없습니다. 주소·인터넷·교내망/VPN을 확인하세요.":e.Message;}finally{busy=false;body.IsEnabled=!resuming;EnableActions(!resuming);}}
    bool Current(MessengerApi source,int version)=>api==source&&version==epoch&&!covered;
    void Invalidate(){epoch++;MainThread.BeginInvokeOnMainThread(()=>{while(Navigation.NavigationStack.Count>1)Navigation.RemovePage(Navigation.NavigationStack.Last());Login();});}
    void Login()
    {
        updateNoticeOffered=false;
        Header("학교에서 전하는 이야기","우리 학교 소식");body.Add(Text("가정통신문과 공지를 확인하세요. 교직원 업무 메시지는 교내망 또는 학교 VPN에서 열 수 있습니다.",14));
        var mode=new Picker{Title="접속할 공간",ItemsSource=new[]{"학교 소식 · 학생/보호자/교사","교직원 교무실 · 교내망/VPN"},SelectedIndex=Preferences.Default.Get("mode",0)};body.Add(mode);
        var address=Input("학교에서 안내한 서버 주소",value:Preferences.Default.Get("address-"+mode.SelectedIndex,""));address.Keyboard=Keyboard.Url;
        mode.SelectedIndexChanged+=(_,_)=>address.Text=Preferences.Default.Get("address-"+mode.SelectedIndex,"");
        var username=Input("아이디");username.Keyboard=Keyboard.Text;var password=Input("비밀번호",true);var remember=new Switch{IsToggled=true};body.Add(Text("자동 로그인 · 비밀번호는 저장하지 않습니다.",12));body.Add(remember);
        async Task Connect(bool restore)
        {
            api?.Dispose();epoch++;var source=new MessengerApi(address.Text??"",mode.SelectedIndex==1);api=source;source.Invalidated+=()=>{if(api==source)Invalidate();};
            Preferences.Default.Set("mode",mode.SelectedIndex);Preferences.Default.Set("address-"+mode.SelectedIndex,source.Address.AbsoluteUri);
            if(restore){await source.Restore();await source.Refresh();if(source.Session.GetProperty("user").ValueKind==JsonValueKind.Null)throw new InvalidOperationException("저장된 로그인이 없습니다. 아이디·비밀번호로 로그인하세요.");}
            else{try{await source.Login(username.Text??"",password.Text??"",remember.IsToggled);}finally{password.Text="";}}
            await Home();
        }
        Action("로그인",()=>Connect(false),true);Action("저장된 로그인으로 접속",()=>Connect(true));
        Action("학교 초대 코드로 가입",async()=>{if(mode.SelectedIndex==1)throw new InvalidOperationException("교직원 계정 신청은 PC 교무실에서 진행하세요.");var source=new MessengerApi(address.Text??"",false);var p=new ContentPage{Title="학교 초대로 가입",BackgroundColor=BackgroundColor};var stack=new VerticalStackLayout{Padding=24,Spacing=16};p.Content=new ScrollView{Content=stack};stack.Add(Text("이름·역할·자녀는 학교에서 확인합니다.",15));var code=new Entry{Placeholder="학교 초대 코드"};var id=new Entry{Placeholder="아이디 (3~32자)"};var pass=new Entry{Placeholder="비밀번호 (12~128자)",IsPassword=true};var error=Text("",13);stack.Add(code);stack.Add(id);stack.Add(pass);stack.Add(error);var join=new Button{Text="계정 등록",BackgroundColor=Blue,TextColor=Colors.White};join.Clicked+=async(_,_)=>{join.IsEnabled=false;try{await source.Refresh();await source.Send("api/register",new MobileRegistration(code.Text,id.Text,pass.Text));pass.Text="";await Navigation.PopAsync();feedback.Text="계정 등록 완료. 로그인하세요.";}catch(Exception e){error.Text=e.Message;}finally{join.IsEnabled=true;}};stack.Add(join);p.Disappearing+=(_,_)=>source.Dispose();await Navigation.PushAsync(p);});
    }
    public void Cover(){covered=true;epoch++;foreach(var p in Navigation.NavigationStack.OfType<ContentPage>())p.Content.IsVisible=false;}
    public async Task Resume(){var preserve=preserveComposerOnResume&&composer is not null&&Navigation.NavigationStack.Contains(composer);covered=false;if(api is null){foreach(var p in Navigation.NavigationStack.OfType<ContentPage>())p.Content.IsVisible=true;return;}resuming=true;body.IsEnabled=false;EnableActions(false);try{await api.Refresh();if(api.Session.ValueKind!=JsonValueKind.Undefined&&api.Session.GetProperty("user").ValueKind!=JsonValueKind.Null){if(!preserve){while(Navigation.NavigationStack.Count>1)await Navigation.PopAsync(false);await Home();}}}catch(Exception e){Invalidate();feedback.Text=e.Message;}finally{preserveComposerOnResume=false;resuming=false;body.IsEnabled=!busy;EnableActions(!busy);foreach(var p in Navigation.NavigationStack.OfType<ContentPage>())p.Content.IsVisible=true;}}
    async Task Home()
    {
        var source=api!;var user=source.Session.GetProperty("user");var version=++epoch;
        Header(user.GetProperty("name").GetString()!,source.Office?"온라인 교무실":"학교 소식");
        Action("새로고침",async()=>{await source.Refresh();await Home();});Action("로그아웃",async()=>{try{await source.Logout();}finally{source.Dispose();if(api==source)api=null;Login();}});
        Action("업데이트 내역",()=>ShowUpdates(true));
        await ShowUpdates();
        if(!Current(source,version))return;
        if(source.Office)
        {
            var picker=new Picker{Title="업무",ItemsSource=new[]{"받은 메시지","보낸 메시지","미확인 메시지"},SelectedIndex=view=="sent"?1:view=="unread"?2:0};body.Add(picker);picker.SelectedIndexChanged+=async(_,_)=>{view=picker.SelectedIndex==1?"sent":picker.SelectedIndex==2?"unread":"received";offset=0;await Run(Home);};
            var rows=await source.Get("api/messages?box="+view+"&offset="+offset);if(!Current(source,version))return;
            foreach(var row in rows.EnumerateArray()){var id=row.GetProperty("id").GetString()!;Action(row.GetProperty("title").GetString()!+"\n"+row.GetProperty("senderName").GetString(),()=>OfficeDetail(id));}
            if(rows.GetArrayLength()==0)body.Add(Text("표시할 메시지가 없습니다."));if(rows.GetArrayLength()==100)Action("다음 목록",async()=>{offset+=100;await Home();});
            Action("학생·보호자 공지 작성 / 발송 현황",()=>Compose(true));return;
        }
        var role=user.GetProperty("role").GetString();
        if(role=="admin"){body.Add(Text("계정·학급·보호자 연결은 학교 소식 관리자 웹 화면에서 관리하세요."));Action("관리 화면 열기",()=>Launcher.Default.OpenAsync(source.Address).ContinueWith(_=>{}));return;}
        if(role=="parent")
        {
            var children=await source.Get("api/children");if(!Current(source,version))return;var options=children.EnumerateArray().ToArray();var picker=new Picker{Title="자녀",ItemsSource=new[]{"전체 자녀"}.Concat(options.Select(x=>x.GetProperty("name").GetString()+" · "+x.GetProperty("className").GetString())).ToArray(),SelectedIndex=Array.FindIndex(options,x=>x.GetProperty("id").GetString()==child)+1};body.Add(picker);picker.SelectedIndexChanged+=async(_,_)=>{child=picker.SelectedIndex<=0?"":options[picker.SelectedIndex-1].GetProperty("id").GetString()!;offset=0;await Run(Home);};
        }
        var filter=new Switch{IsToggled=unread};body.Add(Text("미확인 공지만 보기",12));body.Add(filter);filter.Toggled+=async(_,e)=>{unread=e.Value;await Run(Home);};
        if(role=="teacher")Action("새 공지 작성",()=>Compose(false),true);
        var notices=await source.Get("api/announcements?offset="+offset+(child.Length>0?"&childId="+child:""));if(!Current(source,version))return;var visible=0;
        foreach(var row in notices.EnumerateArray()){if(unread&&row.GetProperty("readAt").ValueKind!=JsonValueKind.Null)continue;visible++;var id=row.GetProperty("id").GetString()!;Action((row.GetProperty("readAt").ValueKind==JsonValueKind.Null?"● ":"")+row.GetProperty("title").GetString()+"\n"+row.GetProperty("senderName").GetString()+" · "+Date(row.GetProperty("publishedAt").GetInt64()),()=>NoticeDetail(id));}
        if(visible==0)body.Add(Text("표시할 공지가 없습니다. 새로고침으로 학교 소식을 확인하세요."));if(notices.GetArrayLength()==100)Action("다음 목록",async()=>{offset+=100;await Home();});
    }
    static string Date(long value)=>DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().ToString("MM.dd HH:mm");
    async Task ShowUpdates(bool manual=false)
    {
        if(updateNoticeOpen||(!manual&&updateNoticeOffered))return;
        try
        {
            var notice=SchoolMessenger.Shared.ReleaseNotice.Load("mobile");
            var confirmed=Preferences.Default.Get("update-confirmed-sequence",0);
            if(!manual&&!notice.ShouldShow(confirmed))return;
            updateNoticeOffered=true;
            updateNoticeOpen=true;
            if(await DisplayAlertAsync("업데이트 내역 · "+notice.Version,notice.Text,"확인","나중에"))
                Preferences.Default.Set("update-confirmed-sequence",Math.Max(confirmed,notice.Sequence));
        }
        catch(Exception e) when(e is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {feedback.Text="업데이트 내역을 읽거나 확인 기록을 저장하지 못했습니다.";}
        finally{updateNoticeOpen=false;}
    }
    async Task OfficeDetail(string id)
    {
        var source=api!;var version=epoch;var value=await source.Get("api/messages/"+id);if(!Current(source,version))return;
        var page=Detail(value.GetProperty("message").GetProperty("title").GetString()!,value.GetProperty("message").GetProperty("body").GetString()!,value.GetProperty("attachments"),source);
        if(value.GetProperty("message").GetProperty("senderId").GetString()!=source.Session.GetProperty("user").GetProperty("id").GetString())await source.Send("api/messages/"+id+"/read");if(Current(source,version))await Navigation.PushAsync(page);
    }
    ContentPage Detail(string title,string text,JsonElement files,MessengerApi source)
    {
        var page=new ContentPage{Title=title,BackgroundColor=BackgroundColor};var stack=new VerticalStackLayout{Padding=24,Spacing=18};page.Content=new ScrollView{Content=stack};stack.Add(Text(title,24,true));stack.Add(Text(text,16));
        stack.Add(Text("첨부는 게시·전송일부터 30일간 보관됩니다. 저장한 사본은 직접 관리하세요.",12));
        foreach(var file in files.EnumerateArray()){var expired=file.TryGetProperty("expired",out var flag)&&flag.GetBoolean();var id=file.GetProperty("id").GetString()!;var name=file.GetProperty("name").GetString()!;var b=new Button{Text=name+(expired?" · 기간 만료":" · 저장/공유"),IsEnabled=!expired};b.Clicked+=async(_,_)=>{b.IsEnabled=false;try{await source.ShareFile(id,name);}catch(Exception e){await page.DisplayAlertAsync("첨부파일",e.Message,"확인");}finally{b.IsEnabled=true;}};stack.Add(b);}return page;
    }
    async Task NoticeDetail(string id)
    {
        var source=api!;var version=epoch;var value=await source.Get("api/announcements/"+id);if(!Current(source,version))return;var notice=value.GetProperty("notice");var page=Detail(notice.GetProperty("title").GetString()!,notice.GetProperty("body").GetString()!,value.GetProperty("attachments"),source);var stack=(VerticalStackLayout)((ScrollView)page.Content).Content;
        if(source.Session.GetProperty("user").GetProperty("role").GetString()=="teacher")
        {
            stack.Add(Text("확인 "+notice.GetProperty("readCount")+" / "+notice.GetProperty("recipientCount"),13));foreach(var receipt in value.GetProperty("receipts").EnumerateArray())stack.Add(Text(receipt.GetProperty("name").GetString()+" · "+receipt.GetProperty("studentName").GetString()+" · "+(receipt.GetProperty("revoked").GetBoolean()?"접근 회수":receipt.GetProperty("readAt").ValueKind==JsonValueKind.Null?"미확인":"확인"),13));
            if(!notice.GetProperty("withdrawn").GetBoolean()){var b=new Button{Text="공지 회수"};b.Clicked+=async(_,_)=>{if(!await page.DisplayAlertAsync("공지 회수","수신자의 공지·첨부 접근을 회수할까요?","회수","취소"))return;b.IsEnabled=false;try{await source.Send("api/announcements/"+id+"/retract");await Navigation.PopAsync();await Home();}catch(Exception e){await page.DisplayAlertAsync("공지 회수",e.Message,"확인");b.IsEnabled=true;}};stack.Add(b);}
        }
        else await source.Send("api/announcements/"+id+"/read");if(Current(source,version))await Navigation.PushAsync(page);
    }
    async Task Compose(bool office)
    {
        var source=api!;var version=epoch;var setup=await source.Get(office?"api/external-announcements/setup":"api/session");
        if(office&&(!setup.GetProperty("connected").GetBoolean()||setup.GetProperty("teacherId").ValueKind==JsonValueKind.Null))throw new InvalidOperationException("공지 관리자에게 교내 계정 연결·외부 서버 설정을 요청하세요.");
        var rooms=office?setup.GetProperty("classes"):await source.Get("api/classes");if(!Current(source,version))return;
        var page=new ContentPage{Title="학생·보호자 공지",BackgroundColor=BackgroundColor};composer=page;var stack=new VerticalStackLayout{Padding=24,Spacing=16};page.Content=new ScrollView{Content=stack};stack.Add(Text("학교 밖 학생·보호자에게 전달됩니다.",13,true));
        var title=new Entry{Placeholder="공지 제목 (200자 이내)",MaxLength=200};var text=new Editor{Placeholder="공지 내용",AutoSize=EditorAutoSizeOption.TextChanges,MinimumHeightRequest=180,MaxLength=10000};stack.Add(title);stack.Add(text);var audience=new Picker{Title="대상",ItemsSource=new[]{"학생과 보호자","보호자","학생"},SelectedIndex=0};stack.Add(audience);
        var scopes=new Dictionary<string,CheckBox>();foreach(var room in rooms.EnumerateArray()){var c=new CheckBox();scopes.Add(room.GetProperty("id").GetString()!,c);stack.Add(new HorizontalStackLayout{Children={c,Text(room.GetProperty("name").GetString()!,14)}});}
        var whole=new CheckBox();var canAll=office?setup.GetProperty("canBroadcast").GetBoolean():setup.GetProperty("user").GetProperty("canBroadcast").GetBoolean();if(canAll)stack.Add(new HorizontalStackLayout{Children={whole,Text("학교 전체 발송",14)}});
        var error=Text("",13);stack.Add(error);var send=new Button{Text="공지 보내기",BackgroundColor=Blue,TextColor=Colors.White};var files=new List<string>();var attach=new Button{Text="공지 첨부 추가 · 파일당 100MB"};attach.Clicked+=async(_,_)=>{attach.IsEnabled=send.IsEnabled=false;try{if(files.Count>=10)throw new InvalidOperationException("첨부는 최대 10개입니다.");preserveComposerOnResume=true;var file=await FilePicker.Default.PickAsync();if(file is null)return;var uploaded=await source.Upload(file,true);files.Add(uploaded.GetProperty("id").GetString()!);stack.Add(Text(file.FileName,12));}catch(Exception e){error.Text=e.Message;}finally{attach.IsEnabled=send.IsEnabled=true;}};stack.Add(attach);
        var client=Guid.NewGuid().ToString("N");AnnouncementRequest? pending=null;send.Clicked+=async(_,_)=>{send.IsEnabled=attach.IsEnabled=false;try{if(pending is null){if(string.IsNullOrWhiteSpace(title.Text)||string.IsNullOrWhiteSpace(text.Text)||!whole.IsChecked&&!scopes.Values.Any(c=>c.IsChecked))throw new InvalidOperationException("제목·내용·수신 학급을 입력하세요.");if(!await page.DisplayAlertAsync("학교 밖으로 공지 발송","수신 대상과 내용을 확인했나요?","발송","취소"))return;pending=new(client,title.Text??"",text.Text??"",audience.SelectedIndex==1?"parents":audience.SelectedIndex==2?"students":"both",whole.IsChecked?[]:scopes.Where(c=>c.Value.IsChecked).Select(c=>c.Key).ToArray(),whole.IsChecked,files.ToArray());}await source.Send(office?"api/external-announcements":"api/announcements",pending);await Navigation.PopAsync();await Home();feedback.Text=office?"발송 대기열에 등록했습니다. 외부 서버 확인 후 게시됩니다.":"공지가 게시되었습니다.";}catch(Exception e){error.Text=e.Message;if(pending is not null){title.IsEnabled=text.IsEnabled=audience.IsEnabled=attach.IsEnabled=false;foreach(var c in scopes.Values)c.IsEnabled=false;whole.IsEnabled=false;send.Text="같은 공지 다시 전송";}}finally{send.IsEnabled=true;attach.IsEnabled=pending is null;}};stack.Add(send);
        if(office){var rows=await source.Get("api/external-announcements");foreach(var row in rows.EnumerateArray()){var id=row.GetProperty("id").GetString()!;stack.Add(Text(row.GetProperty("request").GetProperty("title").GetString()+" · "+State(row.GetProperty("state").GetString()!),14,true));if(row.GetProperty("error").ValueKind==JsonValueKind.String)stack.Add(Text(row.GetProperty("error").GetString()!,12));if(row.GetProperty("detail").ValueKind!=JsonValueKind.Null){var n=row.GetProperty("detail").GetProperty("notice");stack.Add(Text("확인 "+n.GetProperty("readCount")+" / "+n.GetProperty("recipientCount"),12));}if(row.GetProperty("state").GetString()!="withdrawn"){var b=new Button{Text="이 공지 회수 요청"};b.Clicked+=async(_,_)=>{try{if(await page.DisplayAlertAsync("공지 회수","연결이 복구되면 수신자의 접근을 회수합니다.","요청","취소")){await source.Send("api/external-announcements/"+id+"/retract");b.IsEnabled=false;error.Text="회수 요청을 등록했습니다.";}}catch(Exception e){error.Text=e.Message;}};stack.Add(b);}}}await Navigation.PushAsync(page);
    }
    static string State(string value)=>value switch{"pending"=>"전송 대기","published"=>"게시됨","needs-review"=>"대상·권한 재확인 필요","withdraw-pending"=>"회수 대기","withdrawn"=>"회수됨",_=>value};
}
