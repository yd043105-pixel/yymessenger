using System.Globalization;
using System.Text.Json;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    async Task<bool> Timetable(string? selectedDate=null)
    {
        var source=api!;var version=epoch;var setup=await source.Get("api/timetable/setup");if(!Current(source,version))return false;
        var teacher=source.Office||source.Session.GetProperty("user").GetProperty("role").GetString()=="teacher";
        var rooms=setup.GetProperty("classes").EnumerateArray().ToArray();
        var page=new ContentPage{Title=teacher?"시간표 · 내 수업":"우리 학급 시간표",BackgroundColor=BackgroundColor};
        var stack=new VerticalStackLayout{Padding=24,Spacing=16};page.Content=new ScrollView{Content=stack};
        var date=new DatePicker{Date=selectedDate is null?DateTime.UtcNow.AddHours(9).Date:DateTime.ParseExact(selectedDate,"yyyy-MM-dd",CultureInfo.InvariantCulture)};
        var scopes=(teacher?new[]{"내 수업"}:Array.Empty<string>()).Concat(rooms.Select(r=>r.GetProperty("name").GetString()!)).ToArray();
        var scope=new Picker{Title="조회할 시간표",ItemsSource=scopes,SelectedIndex=scopes.Length>0?0:-1};
        stack.Add(Text(teacher?"본인 수업 또는 학급을 선택하세요.":"학생별 수강 목록 등록 전에는 학급 시간표를 보여줍니다.",13));stack.Add(date);stack.Add(scope);
        var error=Text("",13);stack.Add(error);var dayButton=new Button{Text="선택한 날짜 보기",BackgroundColor=Blue,TextColor=Colors.White};var weekButton=new Button{Text="선택한 주 보기"};stack.Add(dayButton);stack.Add(weekButton);
        var content=new VerticalStackLayout{Spacing=14};stack.Add(content);bool loading=false,loaded=false;
        async Task Show(bool weekly)
        {
            if(loading||!Current(source,version))return;loading=true;loaded=false;dayButton.IsEnabled=weekButton.IsEnabled=false;error.Text="";content.Clear();
            try
            {
                if(scope.SelectedIndex<0||date.Date is not{} selected)throw new InvalidOperationException("학급과 날짜를 선택하세요.");
                var mine=teacher&&scope.SelectedIndex==0;var room=mine?null:rooms[scope.SelectedIndex-(teacher?1:0)].GetProperty("id").GetString();
                var first=weekly?selected.AddDays(-((int)selected.DayOfWeek+6)%7):selected;long? revision=null;var days=new List<JsonElement>();
                for(var n=0;n<(weekly?7:1);n++)
                {
                    var value=await source.Get("api/timetable/day?date="+first.AddDays(n).ToString("yyyy-MM-dd")+(mine?"&mine=true":"&classId="+room));if(!Current(source,version))return;
                    var rev=value.GetProperty("revision").GetInt64();if(revision is not null&&revision!=rev)throw new InvalidOperationException("조회 중 시간표가 변경되었습니다. 다시 조회하세요.");revision=rev;days.Add(value);
                }
                foreach(var value in days)
                {
                    content.Add(Text(value.GetProperty("date").GetString()!,20,true));
                    var published=value.GetProperty("publishedAt").GetInt64();if(published>0)content.Add(Text("최종 게시 "+DateTimeOffset.FromUnixTimeMilliseconds(published).ToOffset(TimeSpan.FromHours(9)).ToString("MM.dd HH:mm")+" · 버전 "+revision,12));
                    if(value.GetProperty("missingClasses").GetArrayLength()>0)content.Add(Text("아직 시간표가 등록되지 않은 학급이 있습니다.",13));
                    var slots=value.GetProperty("slots").EnumerateArray().ToArray();
                    if(mine&&slots.Length==0)content.Add(Text("이 날짜에 배정된 수업이 없습니다.",14));
                    foreach(var slot in slots)
                    {
                        var subject=slot.GetProperty("subject").GetString();var changed=slot.GetProperty("changed").GetBoolean();
                        var label=slot.GetProperty("period").GetInt32()+"교시 · "+(mine?slot.GetProperty("className").GetString()+" · ":"")+(string.IsNullOrEmpty(subject)?"수업 없음":subject+" · "+slot.GetProperty("teacherName").GetString());
                        if(changed)label+="\n변경 전: "+(slot.GetProperty("beforeSubject").GetString() is{Length:>0} before?before+" · "+slot.GetProperty("beforeTeacher").GetString():"수업 없음");
                        var line=Text(label,15,changed);if(changed)line.TextColor=Blue;content.Add(line);
                    }
                    content.Add(Text(slots.Any(s=>s.GetProperty("source").GetString()=="daily")?"일자별 시간표 적용":"기초시간표 적용",12));
                }
                loaded=true;
            }
            catch(Exception e){content.Clear();error.Text=e is HttpRequestException?"서버에 연결할 수 없습니다. 최신 시간표를 다시 조회하세요.":e.Message;}
            finally{loading=false;dayButton.IsEnabled=weekButton.IsEnabled=true;}
        }
        dayButton.Clicked+=async(_,_)=>await Show(false);weekButton.Clicked+=async(_,_)=>await Show(true);
        await Show(false);if(!Current(source,version))return false;await Navigation.PushAsync(page);return loaded;
    }
}
