using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR.Client;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    HubConnection? desktopHub;
    MessengerApi? desktopOwner;
    readonly Label desktopStatus=Text("로그인 필요",12);
    partial void ConfigureDesktop()
    {
        var content=Content;var sidebar=new VerticalStackLayout{Padding=16,Spacing=8,BackgroundColor=Colors.White};
        sidebar.Add(new Image{Source="brand_mark.png",WidthRequest=42,HeightRequest=42,HorizontalOptions=LayoutOptions.Start});
        sidebar.Add(Text("교무톡",22,true));sidebar.Add(desktopStatus);
        void Link(string label,Func<Task> action)
        {
            var button=MakeAction(label,async()=>{if(api?.Session.ValueKind!=JsonValueKind.Object||api.Session.GetProperty("user").ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("학교 계정으로 먼저 로그인하세요.");await action();});
            button.HorizontalOptions=LayoutOptions.Fill;button.FontSize=14;sidebar.Add(button);
        }
        Link("받은 메시지",async()=>{homeTab="notices";view="received";offset=0;await Home();});
        Link("보낸 메시지",async()=>{homeTab="notices";view="sent";offset=0;await Home();});
        Link("새 메시지",()=>MacCompose());Link("조직도",MacPeople);
        Link("내 시간표",async()=>{await Timetable();});Link("학교 일정",()=>OfficeSchedule(false));
        Link("특별실 예약",()=>OfficeSchedule(true));Link("상담 예약",Counseling);
        Link("내 정보",async()=>{homeTab="account";await Home();});
        Link("자리비움 / 복귀",async()=>{if(desktopHub?.State!=HubConnectionState.Connected)throw new InvalidOperationException("연결을 확인하세요.");desktopAway=!desktopAway;await desktopHub.InvokeAsync("SetAway",desktopAway);desktopStatus.Text=desktopAway?"자리비움":"온라인";});
        var grid=new Grid{ColumnDefinitions={new(new GridLength(184)),new(GridLength.Star)},ColumnSpacing=1};
        grid.Add(new ScrollView{Content=sidebar});grid.Add(content,1);Content=grid;
        body.MaximumWidthRequest=900;body.HorizontalOptions=LayoutOptions.Fill;
    }
    bool desktopAway;
    async Task StartDesktopPresence()
    {
        var source=api;if(source?.Office!=true)return;
        if(desktopOwner==source&&desktopHub is not null)return;
        StopDesktopPresence();desktopOwner=source;
        var hub=new HubConnectionBuilder().WithUrl(new Uri(source.Address,"hub"),options=>
        {options.Cookies=source.SessionCookies;options.HttpMessageHandlerFactory=handler=>{if(handler is HttpClientHandler client)client.AllowAutoRedirect=false;return handler;};}).WithAutomaticReconnect().Build();
        desktopHub=hub;
        hub.On<JsonElement>("NewMessage",_=>MainThread.BeginInvokeOnMainThread(()=>{if(desktopOwner==source)desktopStatus.Text="새 메시지가 도착했습니다";}));
        hub.Reconnecting+=_=>{MainThread.BeginInvokeOnMainThread(()=>desktopStatus.Text="다시 연결 중");return Task.CompletedTask;};
        hub.Reconnected+=_=>{MainThread.BeginInvokeOnMainThread(()=>desktopStatus.Text="온라인");return Task.CompletedTask;};
        hub.Closed+=_=>{MainThread.BeginInvokeOnMainThread(()=>{if(desktopHub==hub)desktopStatus.Text="연결 끊김 · 받은함 새로고침";});return Task.CompletedTask;};
        try{await hub.StartAsync();if(desktopOwner==source)desktopStatus.Text=source.StorageWarning??"온라인";}
        catch(Exception error)when(error is HttpRequestException or InvalidOperationException or OperationCanceledException)
        {if(desktopHub==hub){desktopHub=null;desktopOwner=null;desktopStatus.Text="실시간 연결 실패 · 받은함으로 재시도";}await hub.DisposeAsync();}
    }
    void StopDesktopPresence()
    {
        var hub=desktopHub;desktopHub=null;desktopOwner=null;desktopAway=false;desktopStatus.Text="로그인 필요";
        if(hub is not null)_=DisposeDesktopHub(hub);
    }
    static async Task DisposeDesktopHub(HubConnection hub){try{await hub.DisposeAsync();}catch(Exception error)when(error is HttpRequestException or OperationCanceledException or InvalidOperationException){}}
    static async Task<JsonElement> MacSend(MessengerApi source,string path,JsonObject payload)
    {using var content=new StringContent(payload.ToJsonString(),Encoding.UTF8,"application/json");return await source.Send(path,content:content);}
    async Task MacPeople()
    {
        var source=api!;var version=epoch;var people=await source.Get("api/people");if(!Current(source,version))return;
        var stack=new VerticalStackLayout{Padding=24,Spacing=12};var page=new ContentPage{Title="조직도",Content=new ScrollView{Content=stack},BackgroundColor=BackgroundColor};
        var search=new SearchBar{Placeholder="이름·부서 검색"};stack.Add(search);var rows=new VerticalStackLayout{Spacing=8};stack.Add(rows);
        void Show()
        {
            rows.Clear();foreach(var person in people.EnumerateArray())
            {
                var name=person.GetProperty("name").GetString()!;var department=person.GetProperty("department").GetString()??"";
                if(!(name+department).Contains(search.Text??"",StringComparison.OrdinalIgnoreCase))continue;
                var id=person.GetProperty("id").GetString()!;var b=MakeAction(name+" · "+department,()=>MacCompose(id));rows.Add(b);
            }
        }
        search.TextChanged+=(_,_)=>Show();Show();await Navigation.PushAsync(page);
    }
    async Task MacCompose(string? recipient=null)
    {
        var source=api!;var version=epoch;var people=await source.Get("api/people");if(!Current(source,version))return;
        var page=new ContentPage{Title="새 메시지",BackgroundColor=BackgroundColor};composer=page;
        var stack=new VerticalStackLayout{Padding=24,Spacing=12};var grid=new Grid{RowDefinitions={new(GridLength.Star),new(GridLength.Auto)}};
        grid.Add(new ScrollView{Content=stack});page.Content=grid;
        var recipients=new Dictionary<string,CheckBox>();var recipientRows=new VerticalStackLayout{Spacing=2};
        foreach(var person in people.EnumerateArray())
        {
            var id=person.GetProperty("id").GetString()!;var check=new CheckBox{IsChecked=id==recipient};recipients[id]=check;
            recipientRows.Add(new HorizontalStackLayout{Children={check,Text(person.GetProperty("name").GetString()+" · "+person.GetProperty("department").GetString(),14)}});
        }
        stack.Add(Text("받는 사람",13,true));stack.Add(new ScrollView{Content=recipientRows,HeightRequest=108});
        var self=MakeAction("나에게 전송",()=>{foreach(var pair in recipients)pair.Value.IsChecked=pair.Key==source.Session.GetProperty("user").GetProperty("id").GetString();return Task.CompletedTask;});stack.Add(self);
        var title=new Entry{Placeholder="제목 (비우면 본문으로 표시)",MaxLength=200};var text=new Editor{Placeholder="메시지를 입력하세요",MinimumHeightRequest=240,MaxLength=10000,AutoSize=EditorAutoSizeOption.TextChanges};stack.Add(title);stack.Add(text);
        var files=new List<string>();var names=new VerticalStackLayout{Spacing=4};stack.Add(names);var error=Text("",13);stack.Add(error);
        var attach=MakeAction("파일 첨부",async()=>
        {
            preserveComposerOnResume=true;var file=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="메시지 첨부파일 선택"});if(file is null)return;
            var upload=await source.Upload(file,false);if(!Current(source,version))return;files.Add(upload.GetProperty("id").GetString()!);names.Add(Text(file.FileName,12));
        });stack.Add(attach);
        var client=Guid.NewGuid().ToString("N");JsonObject? pending=null;
        var send=new Button{Text="메시지 보내기",BackgroundColor=Blue,TextColor=Colors.White,Margin=16,HorizontalOptions=LayoutOptions.End};grid.Add(send,0,1);
        send.Clicked+=async(_,_)=>
        {
            if(api!=source||!Current(source,version))return;
            send.IsEnabled=attach.IsEnabled=false;
            try
            {
                if(pending is null)
                {
                    var ids=recipients.Where(p=>p.Value.IsChecked).Select(p=>p.Key).ToArray();
                    if(ids.Length==0||string.IsNullOrWhiteSpace(text.Text))throw new InvalidOperationException("받는 사람과 메시지를 입력하세요.");
                    pending=new JsonObject{["clientId"]=client,["title"]=title.Text??"",["body"]=text.Text,["recipientIds"]=new JsonArray(ids.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["attachmentIds"]=new JsonArray(files.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["all"]=false};
                    title.IsEnabled=text.IsEnabled=recipientRows.IsEnabled=self.IsEnabled=false;
                }
                await MacSend(source,"api/messages",pending);if(api==source){await Navigation.PopAsync();homeTab="notices";view="sent";offset=0;await Home();}
            }
            catch(Exception e){error.Text=e.Message;send.Text=pending is null?"메시지 보내기":"같은 메시지 다시 전송";}
            finally{send.IsEnabled=true;attach.IsEnabled=pending is null;}
        };
        await Navigation.PushAsync(page);
    }
    partial void ConfigureDesktopDetail(ContentPage page,JsonElement value,MessengerApi source)
    {
        var scroll=page.Content;var grid=new Grid{RowDefinitions={new(GridLength.Star),new(GridLength.Auto)}};grid.Add(scroll);
        var sender=value.GetProperty("message").GetProperty("senderId").GetString();
        var reply=MakeAction("답장",()=>MacCompose(sender),true);reply.Margin=16;reply.HorizontalOptions=LayoutOptions.End;
        grid.Add(reply,0,1);page.Content=grid;
    }
}
