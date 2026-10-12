using System.Globalization;
using System.Text.Json;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    async Task ActivateStaff()
    {
        var source=api!;Header("개인 계정의 첫 설정","교직원 계정 활성화");tabs.IsVisible=false;
        body.Add(Text("관리자가 개별 전달한 활성화 번호를 입력하고 개인 비밀번호를 설정하세요. 완료 전에는 업무 자료를 열 수 없습니다.",14));
        var code=Input("개별 활성화 번호");var password=Input("새 개인 비밀번호 · 12~128자",true);var confirm=Input("새 비밀번호 확인",true);
        var policy=await source.Get("api/registration/policy");var agreement=AddPolicies(body,policy);
        Action("계정 활성화",async()=>
        {
            if(password.Text!=confirm.Text)throw new InvalidOperationException("비밀번호 확인이 일치하지 않습니다.");
            try{await source.Send("api/activate",new StaffActivation(code.Text??"",password.Text??"",agreement()));}
            finally{password.Text=confirm.Text="";}
            source.Remember=false;await source.Refresh();source.Dispose();api=null;Login();feedback.Text="활성화 완료. 개인 비밀번호로 로그인하세요.";
        },true);
        Action("로그아웃",async()=>{try{await source.Logout();}finally{source.Dispose();api=null;Login();}});
    }
    static string OfficeDate(JsonElement row,string field)=>DateTimeOffset.FromUnixTimeMilliseconds(row.GetProperty(field).GetInt64()).ToOffset(TimeSpan.FromHours(9)).ToString("M/d HH:mm");
    static string OfficeTimestamp(DateTime date,TimeSpan time)=>new DateTimeOffset(date.Date.Add(time),TimeSpan.FromHours(9)).ToString("yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture);
    async Task OfficeSchedule(bool reservations)
    {
        var source=api!;var version=epoch;if(!source.Office)throw new InvalidOperationException("교사용 교무실 기능입니다.");
        var setup=await source.Get("api/office/setup");if(!Current(source,version))return;
        var page=new ContentPage{Title=reservations?"특별실 예약":"학사력 · 학교 일정",BackgroundColor=BackgroundColor};
        var stack=new VerticalStackLayout{Padding=20,Spacing=12};page.Content=new ScrollView{Content=stack};
        var date=new DatePicker{Date=DateTime.UtcNow.AddHours(9).Date};stack.Add(Text(reservations?"함께 쓰는 특별실":"우리 학교 일정",22,true));stack.Add(date);
        var rooms=setup.GetProperty("rooms").EnumerateArray().Where(r=>r.GetProperty("active").GetBoolean()).ToArray();
        var room=new Picker{Title="특별실",ItemsSource=new[]{"모든 특별실"}.Concat(rooms.Select(r=>r.GetProperty("name").GetString()!)).ToArray(),SelectedIndex=0};if(reservations)stack.Add(room);
        var list=new VerticalStackLayout{Spacing=12};var error=Text("",13);stack.Add(error);bool loading=false;
        async Task Load()
        {
            if(loading||!Current(source,version))return;loading=true;error.Text="";list.Clear();
            try
            {
                var selected=date.Date??DateTime.UtcNow.AddHours(9).Date;var from=reservations?selected.Date:new DateTime(selected.Year,selected.Month,1);var to=reservations?from.AddDays(1):from.AddMonths(1);
                var path="api/office/"+(reservations?"bookings":"events")+"?from="+from.ToString("yyyy-MM-dd")+"&to="+to.ToString("yyyy-MM-dd");if(reservations&&room.SelectedIndex>0)path+="&roomId="+rooms[room.SelectedIndex-1].GetProperty("id").GetString();
                var rows=await source.Get(path);if(!Current(source,version))return;
                foreach(var row in rows.EnumerateArray())
                {
                    var details=new VerticalStackLayout{Spacing=5};details.Add(Text(row.GetProperty("title").GetString()!,17,true));
                    details.Add(Text(OfficeDate(row,"start")+" → "+OfficeDate(row,"end")+" · "+row.GetProperty("ownerName").GetString(),12));
                    if(reservations)details.Add(Text(row.GetProperty("roomName").GetString()!,13));else details.Add(Text(row.GetProperty("description").GetString()!,14));
                    var canEdit=reservations?row.GetProperty("ownerId").GetString()==setup.GetProperty("userId").GetString()||setup.GetProperty("roomManager").GetBoolean():setup.GetProperty("calendarManager").GetBoolean();
                    if(canEdit)
                    {
                        details.Add(MakeAction("일정 변경",()=>OfficeEditor(source,setup,reservations,row,async()=>{await Load();})));
                        details.Add(MakeAction("취소",async()=>{if(await page.DisplayAlertAsync("취소 확인","이 항목을 취소할까요?","취소하기","돌아가기")){await source.Send("api/office/"+(reservations?"bookings":"events")+"/"+row.GetProperty("id").GetString()+"/cancel",new OfficeRevision(row.GetProperty("revision").GetInt32()));await Load();}}));
                    }
                    list.Add(Card(details));
                }
                if(rows.GetArrayLength()==0)list.Add(Text(reservations?"이 날짜에는 예약이 없습니다.":"이 달에는 등록된 일정이 없습니다.",14));
            }
            catch(Exception e){error.Text=e.Message;}finally{loading=false;}
        }
        stack.Add(MakeAction(reservations?"날짜 조회":"선택한 달 조회",Load));
        if(reservations||setup.GetProperty("calendarManager").GetBoolean())stack.Add(MakeAction(reservations?"특별실 예약하기":"학교 일정 등록",()=>OfficeEditor(source,setup,reservations,null,Load),true));
        if(reservations&&setup.GetProperty("roomManager").GetBoolean())stack.Add(MakeAction("특별실 등록",async()=>
        {
            var name=await page.DisplayPromptAsync("특별실 등록","특별실 이름을 입력하세요.");if(string.IsNullOrWhiteSpace(name))return;
            var location=await page.DisplayPromptAsync("위치","특별실 위치를 입력하세요.");if(location is null)return;
            await source.Send("api/office/rooms",new OfficeRoomRequest(name,location));await Navigation.PopAsync();await OfficeSchedule(true);
        }));
        stack.Add(list);await Navigation.PushAsync(page);await Load();
    }
    async Task OfficeEditor(MessengerApi source,JsonElement setup,bool booking,JsonElement? existing,Func<Task> refresh)
    {
        var page=new ContentPage{Title=booking?"특별실 예약":"학교 일정",BackgroundColor=BackgroundColor};var stack=new VerticalStackLayout{Padding=20,Spacing=12};page.Content=new ScrollView{Content=stack};
        var start=existing is{} value?DateTimeOffset.FromUnixTimeMilliseconds(value.GetProperty("start").GetInt64()).ToOffset(TimeSpan.FromHours(9)):DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).AddHours(1);
        var end=existing is{} endValue?DateTimeOffset.FromUnixTimeMilliseconds(endValue.GetProperty("end").GetInt64()).ToOffset(TimeSpan.FromHours(9)):start.AddHours(1);
        var title=new Entry{Placeholder="제목",Text=existing?.GetProperty("title").GetString()};stack.Add(title);
        var description=new Editor{Placeholder="행사·일정 안내",AutoSize=EditorAutoSizeOption.TextChanges,Text=!booking&&existing is{} ev?ev.GetProperty("description").GetString():""};if(!booking)stack.Add(description);
        var first=new DatePicker{Date=start.Date};var last=new DatePicker{Date=end.Date};var firstTime=new TimePicker{Time=start.TimeOfDay};var lastTime=new TimePicker{Time=end.TimeOfDay};
        stack.Add(Text("시작 · 한국 시간",13));stack.Add(first);stack.Add(firstTime);stack.Add(Text("종료 · 한국 시간",13));stack.Add(last);stack.Add(lastTime);
        var allDay=new CheckBox{IsChecked=!booking&&existing is{} day&&day.GetProperty("allDay").GetBoolean()};if(!booking)stack.Add(new HorizontalStackLayout{Children={allDay,Text("종일 · 종료 날짜는 마지막 날의 다음 날",12)}});
        var rooms=setup.GetProperty("rooms").EnumerateArray().Where(r=>r.GetProperty("active").GetBoolean()).ToArray();var room=new Picker{Title="특별실",ItemsSource=rooms.Select(r=>r.GetProperty("name").GetString()!).ToArray(),SelectedIndex=booking&&existing is{} old?Array.FindIndex(rooms,r=>r.GetProperty("id").GetString()==old.GetProperty("roomId").GetString()):rooms.Length>0?0:-1};if(booking)stack.Add(room);
        var error=Text("",13);stack.Add(error);var client=existing is{} previous&&previous.TryGetProperty("clientId",out var oldClient)?oldClient.GetString()!:Guid.NewGuid().ToString("N");
        object? pending=null;var save=new Button{Text="저장",BackgroundColor=Blue,TextColor=Colors.White};
        save.Clicked+=async(_,_)=>
        {
            save.IsEnabled=false;try
            {
                if(pending is null)
                {
                    var a=OfficeTimestamp(first.Date??start.Date,!booking&&allDay.IsChecked?TimeSpan.Zero:firstTime.Time??start.TimeOfDay);var b=OfficeTimestamp(last.Date??end.Date,!booking&&allDay.IsChecked?TimeSpan.Zero:lastTime.Time??end.TimeOfDay);var revision=existing?.GetProperty("revision").GetInt32()??0;
                    if(booking&&room.SelectedIndex<0)throw new InvalidOperationException("관리자가 특별실을 먼저 등록해야 합니다.");
                    pending=booking?new OfficeBookingRequest(client,rooms[room.SelectedIndex].GetProperty("id").GetString()!,title.Text??"",a,b,revision):new OfficeEventRequest(client,title.Text??"",description.Text??"",a,b,allDay.IsChecked,revision);
                }
                await source.Send("api/office/"+(booking?"bookings":"events")+(existing is{} row?"/"+row.GetProperty("id").GetString():""),pending);await Navigation.PopAsync();await refresh();
            }
            catch(Exception e){error.Text=e.Message;pending=null;}finally{save.IsEnabled=true;}
        };
        stack.Add(save);await Navigation.PushAsync(page);
    }
}
