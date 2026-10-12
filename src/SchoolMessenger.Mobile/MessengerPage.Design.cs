using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    static Border Card(View content,double padding=16,Color? color=null)=>new()
    {
        Content=content,Padding=padding,BackgroundColor=color??Colors.White,
        Stroke=Line,StrokeThickness=1,StrokeShape=new RoundRectangle{CornerRadius=16}
    };
    static View Brand()
    {
        var name=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};
        name.Add(Text("여양고등학교",15,true));var caption=Text("학교와 일상을 잇다",10);caption.TextColor=Muted;name.Add(caption);
        var logo=Text("여",19,true);logo.TextColor=Colors.White;logo.HorizontalTextAlignment=TextAlignment.Center;logo.VerticalTextAlignment=TextAlignment.Center;
        var grid=new Grid{ColumnDefinitions={new(GridLength.Auto),new(GridLength.Star)},ColumnSpacing=10,Margin=new Thickness(0,0,0,12)};
        grid.Add(new Border{Content=logo,WidthRequest=36,HeightRequest=36,BackgroundColor=Blue,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=11}});
        grid.Add(name,1);return grid;
    }
    Button MakeAction(string label,Func<Task> action,bool primary=false)
    {
        var button=new Button{Text=label,BackgroundColor=primary?Blue:Colors.White,TextColor=primary?Colors.White:Navy,CornerRadius=12,Padding=new Thickness(14,10),FontSize=14,MinimumHeightRequest=44};
        button.Clicked+=async(_,_)=>await Run(action);return button;
    }
    void NavigationTabs(bool office)
    {
        tabs.Clear();tabs.IsVisible=true;
        var options=new[]{("home","홈","nav_home.png"),("notices",office?"메시지":"소식","nav_notices.png"),("account","내 정보","nav_profile.png")};
        for(var index=0;index<options.Length;index++)
        {
            var (key,label,icon)=options[index];var selected=homeTab==key;
            var item=new Grid{MinimumHeightRequest=56,BackgroundColor=selected?Pale:Colors.Transparent};
            item.Add(new Image{Source=icon,HeightRequest=20,WidthRequest=20,VerticalOptions=LayoutOptions.Start,HorizontalOptions=LayoutOptions.Center,Margin=new Thickness(0,7,0,0)});
            var button=MakeAction(label,async()=>{homeTab=key;offset=0;await Home();});
            button.BackgroundColor=Colors.Transparent;button.TextColor=selected?Blue:Muted;button.FontSize=11;button.FontAttributes=selected?FontAttributes.Bold:FontAttributes.None;button.Padding=new Thickness(2,26,2,2);item.Add(button);
            tabs.Add(new Border{Content=item,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=12}},index);
        }
    }
    void Login()
    {
        homeTab="home";tabs.IsVisible=false;updateNoticeOffered=false;
        Header("우리 학교의 하루를 함께","우리 학교 소식");
        var help=Text("가정통신문부터 시간표까지,\n필요한 학교 소식을 한곳에서 확인하세요.",14);help.TextColor=Muted;help.Margin=new Thickness(0,0,0,6);body.Add(help);
        var mode=new Picker{ClassId="school-field",Title="접속할 공간",ItemsSource=new[]{"학교 소식 · 학생/보호자/교사","교직원 교무실 · 교내망/VPN"},SelectedIndex=Preferences.Default.Get("mode",0)};body.Add(Card(mode,8));
        var address=Input("학교에서 안내한 서버 주소",value:Preferences.Default.Get("address-"+mode.SelectedIndex,""));address.Keyboard=Keyboard.Url;
        mode.SelectedIndexChanged+=(_,_)=>{address.Text=Preferences.Default.Get("address-"+mode.SelectedIndex,"");help.Text=mode.SelectedIndex==1?"교직원 업무 메시지는 교내망 또는\n학교 VPN에 연결한 뒤 확인하세요.":"가정통신문부터 시간표까지,\n필요한 학교 소식을 한곳에서 확인하세요.";};
        var username=Input("아이디");username.Keyboard=Keyboard.Text;var password=Input("비밀번호",true);
        var remember=new Switch{IsToggled=true};var rememberRow=new Grid{ColumnDefinitions={new(GridLength.Star),new(GridLength.Auto)}};
        var rememberLabel=Text("자동 로그인",13);rememberLabel.VerticalOptions=LayoutOptions.Center;rememberRow.Add(rememberLabel);rememberRow.Add(remember,1);body.Add(rememberRow);
        SemanticProperties.SetDescription(remember,"자동 로그인. 비밀번호는 저장하지 않습니다.");
        async Task Connect(bool restore)
        {
            api?.Dispose();epoch++;var source=new MessengerApi(address.Text??"",mode.SelectedIndex==1);api=source;source.Invalidated+=()=>{if(api==source)Invalidate();};
            Preferences.Default.Set("mode",mode.SelectedIndex);Preferences.Default.Set("address-"+mode.SelectedIndex,source.Address.AbsoluteUri);
            if(restore){await source.Restore();await source.Refresh();if(source.Session.GetProperty("user").ValueKind==JsonValueKind.Null)throw new InvalidOperationException("저장된 로그인이 없습니다. 아이디·비밀번호로 로그인하세요.");}
            else{try{await source.Login(username.Text??"",password.Text??"",remember.IsToggled);}finally{password.Text="";}}
            homeTab="home";offset=0;child="";unread=false;await Home();
        }
        Action("로그인",()=>Connect(false),true);
        var restore=Action("저장된 로그인으로 접속",()=>Connect(true));restore.BackgroundColor=Colors.Transparent;restore.TextColor=Muted;restore.FontSize=12;
        var joins=new Grid{ColumnDefinitions={new(GridLength.Star),new(GridLength.Star)},ColumnSpacing=8};
        var signup=MakeAction("학생·학부모 회원가입",()=>mode.SelectedIndex==1?throw new InvalidOperationException("교직원 계정 신청은 PC 교무실에서 진행하세요."):Signup(address.Text??""));signup.FontSize=12;
        var invitation=MakeAction("학교 초대 코드로 가입",()=>mode.SelectedIndex==1?throw new InvalidOperationException("교직원 계정 신청은 PC 교무실에서 진행하세요."):InviteSignup(address.Text??""));invitation.FontSize=12;
        joins.Add(signup);joins.Add(invitation,1);body.Add(joins);
        var privacy=Text("자동 로그인 시 비밀번호는 저장하지 않습니다.",11);privacy.TextColor=Muted;privacy.HorizontalTextAlignment=TextAlignment.Center;body.Add(privacy);
    }
    async Task Home()
    {
        var source=api!;var user=source.Session.GetProperty("user");var version=++epoch;
        var name=user.GetProperty("name").GetString()!;var role=user.GetProperty("role").GetString();
        Header(name+"님, 안녕하세요",homeTab=="account"?"내 정보":source.Office?"온라인 교무실":"학교 소식");NavigationTabs(source.Office);
        await ShowUpdates();if(!Current(source,version))return;
        if(homeTab=="account")
        {
            var info=new VerticalStackLayout{Spacing=5};info.Add(Text(name,18,true));var status=Text(source.Office?"교직원 계정":role switch{"parent"=>"학부모 계정","student"=>"학생 계정","teacher"=>"교사 계정",_=>"관리자 계정"},12);status.TextColor=Muted;info.Add(status);body.Add(Card(info));
            if(!source.Office&&role is "parent" or "student")SettingRow(role=="parent"?"설정 · 자녀 연결":"설정 · 내 고유번호",AccountSettings);
            SettingRow("약관·개인정보 안내",PolicyMenu);SettingRow("업데이트 내역",()=>ShowUpdates(true));
            SettingRow("로그아웃",async()=>{try{await source.Logout();}finally{source.Dispose();if(api==source)api=null;Login();}});return;
        }
        if(!source.Office&&role=="parent"&&user.TryGetProperty("membership",out var membership)&&membership.GetString()=="temporary")
        {
            var info=new VerticalStackLayout{Spacing=10};info.Add(Text("자녀를 연결해주세요",18,true));info.Add(Text("임시회원입니다. 자녀 고유번호를 입력하면 학교 소식과 시간표를 확인할 수 있어요.",14));info.Add(MakeAction("설정 · 자녀 연결",AccountSettings,true));body.Add(Card(info));return;
        }
        if(!source.Office&&role=="admin")
        {
            body.Add(Card(Text("관리자 계정입니다. 계정·학급·보호자 연결은 학교 소식 관리 화면에서 관리하세요.",14)));
            Action("관리 화면 열기",()=>Launcher.Default.OpenAsync(source.Address).ContinueWith(_=>{}));return;
        }
        if(!source.Office&&role=="parent")
        {
            var children=await source.Get("api/children");if(!Current(source,version))return;
            var options=children.EnumerateArray().ToArray();var picker=new Picker{ClassId="school-field",Title="자녀",ItemsSource=new[]{"전체 자녀"}.Concat(options.Select(x=>x.GetProperty("name").GetString()+" · "+x.GetProperty("className").GetString())).ToArray(),SelectedIndex=Array.FindIndex(options,x=>x.GetProperty("id").GetString()==child)+1};
            body.Add(Card(picker,6));picker.SelectedIndexChanged+=async(_,_)=>{child=picker.SelectedIndex<=0?"":options[picker.SelectedIndex-1].GetProperty("id").GetString()!;offset=0;await Run(Home);};
        }
        if(homeTab=="home")
        {
            var today=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
            var daily=new VerticalStackLayout{Spacing=3};var day=Text(today.ToString("M월 d일 dddd",CultureInfo.GetCultureInfo("ko-KR")),12);day.TextColor=Color.FromArgb("#b9c6da");daily.Add(day);
            var timetable=MakeAction("시간표 · 내 수업 / 학급",()=>Timetable());timetable.BackgroundColor=Colors.Transparent;timetable.TextColor=Colors.White;timetable.FontSize=16;timetable.FontAttributes=FontAttributes.Bold;timetable.HorizontalOptions=LayoutOptions.Fill;timetable.Padding=new Thickness(0,6);daily.Add(timetable);
            body.Add(Card(daily,16,Color.FromArgb("#17243b")));
        }
        var scheduleNotices=await source.Get("api/timetable/notices");if(!Current(source,version))return;
        foreach(var notice in scheduleNotices.EnumerateArray().Where(n=>n.GetProperty("readAt").ValueKind==JsonValueKind.Null))
        {
            var revision=notice.GetProperty("revision").GetInt64();var day=notice.GetProperty("date").GetString();
            var room=notice.GetProperty("classIds").EnumerateArray().Select(c=>c.GetString()).FirstOrDefault();
            var change=Action("시간표 변경 · "+notice.GetProperty("title").GetString()+" · "+day,async()=>{if(await Timetable(day,room)&&Current(source,version))await source.Send($"api/timetable/notices/{revision}/read");});change.BackgroundColor=Pale;change.TextColor=Blue;change.FontSize=12;
        }
        var heading=new Grid{ColumnDefinitions={new(GridLength.Star),new(GridLength.Auto)},Margin=new Thickness(0,8,0,0)};
        var section=Text(source.Office?"업무 메시지":homeTab=="home"?"최근 학교 소식":"받은 학교 소식",17,true);section.VerticalOptions=LayoutOptions.Center;heading.Add(section);
        var refresh=MakeAction("새로고침",async()=>{await source.Refresh();await Home();});refresh.BackgroundColor=Colors.Transparent;refresh.TextColor=Muted;refresh.FontSize=12;refresh.Padding=0;heading.Add(refresh,1);body.Add(heading);
        if(source.Office)
        {
            var picker=new Picker{ClassId="school-field",Title="업무",ItemsSource=new[]{"받은 메시지","보낸 메시지","미확인 메시지"},SelectedIndex=view=="sent"?1:view=="unread"?2:0};body.Add(Card(picker,6));picker.SelectedIndexChanged+=async(_,_)=>{view=picker.SelectedIndex==1?"sent":picker.SelectedIndex==2?"unread":"received";offset=0;await Run(Home);};
            var rows=await source.Get("api/messages?box="+view+"&offset="+offset);if(!Current(source,version))return;
            foreach(var row in rows.EnumerateArray()){var id=row.GetProperty("id").GetString()!;Story(row.GetProperty("title").GetString()!,row.GetProperty("senderName").GetString()!,"교직원",false,()=>OfficeDetail(id));}
            if(rows.GetArrayLength()==0)Empty("받은 메시지가 없습니다","새 업무 메시지가 도착하면 여기에 표시됩니다.");if(rows.GetArrayLength()==100)Action("다음 목록",async()=>{offset+=100;await Home();});
            Action("학생·보호자 공지 작성 / 발송 현황",()=>Compose(true));return;
        }
        var filter=new Switch{IsToggled=unread};var filterRow=new Grid{ColumnDefinitions={new(GridLength.Star),new(GridLength.Auto)}};
        var filterLabel=Text("미확인 공지만 보기",12);filterLabel.TextColor=Muted;filterLabel.VerticalOptions=LayoutOptions.Center;filterRow.Add(filterLabel);filterRow.Add(filter,1);body.Add(filterRow);SemanticProperties.SetDescription(filter,"미확인 공지만 보기");filter.Toggled+=async(_,e)=>{unread=e.Value;offset=0;await Run(Home);};
        if(role=="teacher")Action("새 공지 작성",()=>Compose(false),true);
        var notices=await source.Get("api/announcements?offset="+offset+(child.Length>0?"&childId="+child:""));if(!Current(source,version))return;
        var visible=notices.EnumerateArray().Where(row=>!unread||row.GetProperty("readAt").ValueKind==JsonValueKind.Null).ToArray();
        foreach(var row in homeTab=="home"?visible.Take(4):visible)
        {
            var id=row.GetProperty("id").GetString()!;Story(row.GetProperty("title").GetString()!,row.GetProperty("senderName").GetString()!,Date(row.GetProperty("publishedAt").GetInt64()),row.GetProperty("readAt").ValueKind==JsonValueKind.Null,()=>NoticeDetail(id));
        }
        if(visible.Length==0)Empty(unread?"모든 소식을 확인했어요":"아직 도착한 소식이 없어요","학교에서 전달한 소식이 이곳에 표시됩니다.");
        if(homeTab=="home"&&visible.Length>4)Action("학교 소식 전체 보기",async()=>{homeTab="notices";await Home();});
        if(notices.GetArrayLength()==100)Action("다음 목록",async()=>{homeTab="notices";offset+=100;await Home();});
    }
    void SettingRow(string label,Func<Task> action)
    {
        var button=Action(label,action);button.HorizontalOptions=LayoutOptions.Fill;button.TextColor=label=="로그아웃"?Color.FromArgb("#b45353"):Navy;button.FontSize=14;button.Padding=new Thickness(18,14);
    }
    void Story(string title,string sender,string date,bool fresh,Func<Task> action)
    {
        var content=new VerticalStackLayout{Spacing=4};
        var tag=Text(api?.Office==true?"업무 메시지":fresh?"새 소식":"학교 소식",11,true);tag.TextColor=fresh?Blue:Muted;
        content.Add(tag);
        var button=MakeAction(title,action);button.BackgroundColor=Colors.Transparent;button.FontSize=16;button.FontAttributes=FontAttributes.Bold;button.Padding=0;button.LineBreakMode=LineBreakMode.WordWrap;button.HorizontalOptions=LayoutOptions.Start;content.Add(button);
        var metadata=new Grid{ColumnDefinitions={new(GridLength.Star),new(GridLength.Auto)},ColumnSpacing=8};var author=Text(sender,11);author.TextColor=Muted;author.LineBreakMode=LineBreakMode.TailTruncation;author.MaxLines=1;var stamp=Text(date,11);stamp.TextColor=Muted;metadata.Add(author);metadata.Add(stamp,1);content.Add(metadata);body.Add(Card(content));
    }
    void Empty(string title,string description)
    {
        var stack=new VerticalStackLayout{Spacing=6,Padding=new Thickness(0,10)};stack.Add(Text(title,16,true));var text=Text(description,13);text.TextColor=Muted;stack.Add(text);body.Add(Card(stack));
    }
}
