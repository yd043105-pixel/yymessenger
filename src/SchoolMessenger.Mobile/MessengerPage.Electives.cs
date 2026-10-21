using System.Text.Json;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    static string ElectiveTime(long value)=>DateTimeOffset.FromUnixTimeMilliseconds(value).ToOffset(TimeSpan.FromHours(9)).ToString("M월 d일 HH:mm");
    static string ElectiveStatus(ElectiveRound round)=>round.State switch
    {"finalized"=>"분반 확정","closed"=>"신청 마감","open" when round.CanApply=>"신청 가능","open" when round.StartsAt>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()=>"신청 예정",_=>"신청 기간 종료"};
    async Task Electives()
    {
        var source=api!;if(source.Office||source.Session.GetProperty("user").GetProperty("role").GetString()!="student")throw new InvalidOperationException("학생 본인 계정으로 신청하세요.");
        var version=++epoch;Header("나의 수업을 선택하세요","선택과목 신청",true);Action("홈으로",Home);Action("새로고침",Electives);
        var data=await source.Get("api/electives");if(!Current(source,version))return;
        var rounds=JsonSerializer.Deserialize(data,MobileJson.Default.ElectiveRoundArray)??[];
        if(rounds.Length==0){Empty("열린 신청 회차가 없어요","대상 학급과 공개 여부를 학교 담당자에게 확인해 주세요.");return;}
        foreach(var round in rounds)
        {
            var card=new VerticalStackLayout{Spacing=6};card.Add(Text(round.Title,18,true));card.Add(Text(round.Term+" · "+ElectiveStatus(round),13));card.Add(Text(ElectiveTime(round.StartsAt)+" ~ "+ElectiveTime(round.EndsAt)+" · 한국 시각",12));card.Add(Text(round.Application?.Submitted==true?"나의 신청이 접수되었습니다.":"아직 신청하지 않았습니다.",12));
            card.Add(MakeAction("과목·내 신청 보기",()=>ElectiveDetail(round.Id),round.CanApply));body.Add(Card(card));
        }
    }
    async Task ElectiveDetail(string id)
    {
        var source=api!;var version=++epoch;Header("선택과목 신청","신청 내용",true);Action("신청 회차 목록",Electives);
        var data=await source.Get("api/electives/"+Uri.EscapeDataString(id));if(!Current(source,version))return;
        var round=JsonSerializer.Deserialize(data,MobileJson.Default.ElectiveRound)!;var application=round.Application!;
        body.Add(Text(round.Title,21,true));body.Add(Text(round.Term+" · "+ElectiveStatus(round),13));body.Add(Text(ElectiveTime(round.StartsAt)+" ~ "+ElectiveTime(round.EndsAt)+" (한국 시각)",12));body.Add(Text("정원 제한 없이 신청할 수 있어요. 신청 기간에는 변경·취소할 수 있습니다.",13));
        var selection=new VerticalStackLayout{Spacing=12};var checks=new Dictionary<string,CheckBox>();
        foreach(var group in round.Groups)
        {
            var panel=new VerticalStackLayout{Spacing=8};panel.Add(Text(group.Name,17,true));panel.Add(Text("이 묶음에서 "+group.Required+"개 선택",12));var count=Text("",12);count.TextColor=Muted;
            foreach(var course in group.Courses)
            {
                var check=new CheckBox{IsChecked=application.CourseIds.Contains(course.Id),IsEnabled=round.CanApply};checks[course.Id]=check;SemanticProperties.SetDescription(check,group.Name+" · "+course.Name);
                var row=new Grid{ColumnDefinitions={new(GridLength.Auto),new(GridLength.Star)},ColumnSpacing=8};var label=Text(course.Name,15);label.VerticalOptions=LayoutOptions.Center;row.Add(check);row.Add(label,1);panel.Add(row);
                check.CheckedChanged+=(_,_)=>count.Text="선택 "+group.Courses.Count(c=>checks.TryGetValue(c.Id,out var value)&&value.IsChecked)+" / "+group.Required+"개";
            }
            count.Text="선택 "+group.Courses.Count(c=>checks[c.Id].IsChecked)+" / "+group.Required+"개";panel.Add(count);selection.Add(Card(panel));
        }
        body.Add(selection);body.Add(Text(application.Submitted?"접수됨 · "+ElectiveTime(application.UpdatedAt!.Value):"미신청",14,true));
        Action("내 신청 현황 다시 조회",()=>ElectiveDetail(id));
        if(!round.CanApply){body.Add(Text("지금은 신청 내용을 바꿀 수 없습니다.",13));return;}
        ElectiveApplicationRequest? pending=null;
        body.Add(Text("연결이 끊기면 내 신청 현황을 다시 확인한 뒤 진행하세요.",12));
        Action(application.Submitted?"선택 과목 변경":"선택 과목 신청",async()=>
        {
            if(pending is null)
            {
                foreach(var group in round.Groups)if(group.Courses.Count(c=>checks[c.Id].IsChecked)!=group.Required)throw new InvalidOperationException(group.Name+"에서 "+group.Required+"개를 선택하세요.");
                pending=new(Guid.NewGuid().ToString("N"),application.Revision,checks.Where(c=>c.Value.IsChecked).Select(c=>c.Key).ToArray());selection.IsEnabled=false;
            }
            await source.Send("api/electives/"+id+"/application",pending);if(Current(source,version))await ElectiveDetail(id);
        },true);
        if(application.Submitted)
        {
            var cancel=new ElectiveCancellation(Guid.NewGuid().ToString("N"),application.Revision);
            Action("이번 회차 신청 취소",async()=>{if(!await DisplayAlertAsync("신청 취소","선택한 과목 전체를 취소할까요? 기간 안에는 다시 신청할 수 있습니다.","신청 취소","돌아가기"))return;await source.Send("api/electives/"+id+"/cancel",cancel);if(Current(source,version))await ElectiveDetail(id);});
        }
    }
}
