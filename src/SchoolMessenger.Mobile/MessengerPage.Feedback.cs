using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    async Task Feedback()
    {
        var source=api!;var version=epoch;var page=new ContentPage{Title="오류 신고 · 개선 의견",BackgroundColor=BackgroundColor};
        var stack=new VerticalStackLayout{Padding=24,Spacing=12};page.Content=new ScrollView{Content=stack};
        stack.Add(Text("이름·학번·연락처·비밀번호·메시지 본문을 적지 마세요. 학교 서버에만 접수하며 자동으로 공개하지 않습니다.",13));
        var kind=new Picker{Title="종류",ItemsSource=new[]{"오류 신고","개선 의견"},SelectedIndex=0};
        var area=new Picker{Title="기능",ItemsSource=new[]{"로그인","메시지","학교 공지","시간표","상담","특별실","파일","기타"},SelectedIndex=7};
        var title=new Entry{Placeholder="제목 (3~100자)",MaxLength=100};
        var details=new Editor{Placeholder="재현 순서 · 예상 결과 · 실제 결과 (10자 이상)",MaxLength=4000,HeightRequest=150};
        var error=Text("",13);var send=new Button{Text="학교에 접수",BackgroundColor=Blue,TextColor=Colors.White};FeedbackRequest? pending=null;
        send.Clicked+=async(_,_)=>
        {
            if(!Current(source,version))return;send.IsEnabled=false;error.Text="";
            try
            {
                if(pending is null&&(title.Text?.Trim().Length is not >=3||details.Text?.Trim().Length is not >=10))throw new InvalidOperationException("제목 3자, 재현 설명 10자 이상 적어 주세요.");
                pending??=new(Guid.NewGuid().ToString("N"),kind.SelectedIndex==0?"bug":"suggestion",(source.Office?"teacher-":"school-")+(DeviceInfo.Platform==DevicePlatform.iOS?"ios":"android"),new[]{"login","messages","notices","timetable","counseling","rooms","files","other"}[area.SelectedIndex],title.Text??"",details.Text??"",AppInfo.Current.VersionString);
                kind.IsEnabled=area.IsEnabled=title.IsEnabled=details.IsEnabled=false;
                var response=await source.Send("api/feedback",pending);pending=null;await Navigation.PopAsync();feedback.Text="접수 완료 · "+response.GetProperty("id").GetString();
            }
            catch(Exception exception){error.Text=exception.Message;}
            finally{send.IsEnabled=true;}
        };
        foreach(var view in new View[]{kind,area,title,details,send,error})stack.Add(view);
        stack.Add(Text("내 접수 현황",18,true));
        foreach(var row in (await source.Get("api/feedback")).EnumerateArray())stack.Add(Text(row.GetProperty("report").GetProperty("title").GetString()+" · "+(row.GetProperty("status").GetString() switch{"received"=>"접수","investigating"=>"확인 중","verified"=>"수정 대기","deferred"=>"보류","resolved"=>"처리 완료",_=>"확인 중"}),13));
        if(Current(source,version))await Navigation.PushAsync(page);
    }
}
