using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Desktop;

public sealed class ElectiveWindow:Window
{
    readonly Api api;readonly StackPanel body=new(){Margin=new Thickness(22)};readonly TextBlock error=new(){Margin=new Thickness(20,8,20,12),TextWrapping=TextWrapping.Wrap};bool busy,isAdmin;
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    record Choice(string Id,string Name){public override string ToString()=>Name;}
    sealed record CourseEditor(string Id,TextBox Name,ListBox Teachers);
    sealed record GroupEditor(string Id,TextBox Name,TextBox Required,List<CourseEditor> Courses);
    public ElectiveWindow(MainWindow main)
    {
        api=main.Api!;Owner=main;Resources=main.Resources;Title="선택과목 수강신청 관리";Width=820;Height=780;MinHeight=550;MinWidth=640;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var dock=new DockPanel();DockPanel.SetDock(error,Dock.Bottom);dock.Children.Add(error);dock.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=dock;Loaded+=async(_,_)=>await Run(List);
    }
    static TextBlock Text(string value,int size=14)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,5)};
    static TextBox Field(Panel panel,string label,string value=""){panel.Children.Add(Text(label));var field=new TextBox{Text=value,MinWidth=100};panel.Children.Add(field);return field;}
    void Button(Panel panel,string title,Func<Task> action){var button=new Button{Content=title,Margin=new Thickness(0,5,8,5),HorizontalAlignment=HorizontalAlignment.Left};button.Click+=async(_,_)=>await Run(action);panel.Children.Add(button);}
    async Task Run(Func<Task> action){if(busy)return;busy=true;body.IsEnabled=false;error.Text="";try{await action();}catch(Exception e){error.Text=e.Message;}finally{busy=false;body.IsEnabled=true;}}
    public static string State(string value)=>value switch{"draft"=>"초안","open"=>"공개","closed"=>"마감","finalized"=>"분반 확정",_=>value};
    static string Time(long value)=>DateTimeOffset.FromUnixTimeMilliseconds(value).ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm");
    static long ParseTime(string text){if(!DateTime.TryParseExact(text.Trim(),"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new InvalidOperationException("한국 시각으로 yyyy-MM-dd HH:mm 형식으로 입력하세요.");return new DateTimeOffset(DateTime.SpecifyKind(date,DateTimeKind.Unspecified),TimeSpan.FromHours(9)).ToUnixTimeMilliseconds();}
    async Task List()
    {
        var access=await api.Get<JsonElement>("api/elective-management/access");isAdmin=access.GetProperty("isAdmin").GetBoolean();body.Children.Clear();body.Children.Add(Text("선택과목 수강신청",22));body.Children.Add(Text("정원 제한 없음 · 신청 기간 안에 변경·취소 가능"));
        if(isAdmin)Button(body,"수강신청 담당자 권한",Managers);
        if(!access.GetProperty("canManage").GetBoolean()){body.Children.Add(Text("시스템 관리자에게 수강신청 담당자 권한을 요청하세요."));return;}
        Button(body,"새 신청 회차",()=>Editor(null));Button(body,"새로고침",List);
        var rounds=await api.Get<ElectiveRound[]>("api/elective-management");
        if(rounds.Length==0)body.Children.Add(Text("아직 신청 회차가 없습니다. 새 회차에서 대상·기간·과목을 설정하세요."));
        foreach(var round in rounds){var card=new StackPanel{Margin=new Thickness(0,8,0,12)};card.Children.Add(Text(round.Title+" · "+round.Term,17));card.Children.Add(Text(State(round.State)+" · "+Time(round.StartsAt)+" ~ "+Time(round.EndsAt)+" (한국 시각)"));Button(card,"설정·신청 현황",()=>Detail(round.Id));body.Children.Add(card);}
    }
    async Task Managers()
    {
        var users=await api.Get<JsonElement[]>("api/admin/elective-managers");body.Children.Clear();Button(body,"회차 목록",List);body.Children.Add(Text("수강신청 업무 담당자",20));body.Children.Add(Text("지정한 담당자는 전체 회차 설정과 대상 학생의 신청 현황을 볼 수 있습니다."));
        foreach(var user in users){var enabled=user.GetProperty("enabled").GetBoolean();var id=user.GetProperty("id").GetString()!;var check=new CheckBox{Content=user.GetProperty("name").GetString()+" · "+user.GetProperty("department").GetString(),IsChecked=enabled,Margin=new Thickness(0,7,0,3),IsEnabled=!user.GetProperty("isAdmin").GetBoolean()};body.Children.Add(check);if(check.IsEnabled)Button(body,"이 교직원 권한 저장",async()=>{await api.Send<JsonElement>(HttpMethod.Post,"api/admin/elective-managers",new ElectiveManagerChange(id,check.IsChecked==true));await Managers();});}
    }
    async Task Editor(ElectiveRound? round)
    {
        var setup=await api.Get<JsonElement>("api/elective-management/setup");var classes=JsonSerializer.Deserialize<PortalClass[]>(setup.GetProperty("classes"),Json)!;var teachers=setup.GetProperty("teachers").EnumerateArray().Select(t=>new Choice(t.GetProperty("id").GetString()!,t.GetProperty("name").GetString()!)).ToArray();
        body.Children.Clear();Button(body,"저장하지 않고 목록으로",List);body.Children.Add(Text(round is null?"새 신청 회차":"신청 초안 편집",21));body.Children.Add(Text("과목별 정원은 설정하지 않습니다. 공개 이후에는 과목·대상·선택 규칙을 변경할 수 없습니다."));
        var form=new StackPanel();body.Children.Add(form);var title=Field(form,"회차명",round?.Title??"");var term=Field(form,"학년도·학기",round?.Term??(DateTime.Today.Year+"학년도"));
        var tomorrow=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).Date.AddDays(1);
        var start=Field(form,"신청 시작 · 한국 시각 yyyy-MM-dd HH:mm",round is null?tomorrow.ToString("yyyy-MM-dd")+" 09:00":Time(round.StartsAt));var end=Field(form,"신청 종료 · 한국 시각 yyyy-MM-dd HH:mm",round is null?tomorrow.AddDays(7).ToString("yyyy-MM-dd")+" 17:00":Time(round.EndsAt));
        form.Children.Add(Text("대상 학급"));var rooms=new Dictionary<string,CheckBox>();var scopes=new WrapPanel();form.Children.Add(scopes);foreach(var room in classes){var check=new CheckBox{Content=room.Name,IsChecked=round?.ClassIds.Contains(room.Id)==true,Margin=new Thickness(0,5,14,5)};rooms[room.Id]=check;scopes.Children.Add(check);}
        var groups=new List<GroupEditor>();var groupPanel=new StackPanel();form.Children.Add(groupPanel);
        void AddGroup(ElectiveGroup? original)
        {
            var panel=new StackPanel{Margin=new Thickness(0,12,0,8)};var border=new Border{Padding=new Thickness(14),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=panel};border.SetResourceReference(Border.BorderBrushProperty,"Line");groupPanel.Children.Add(border);
            var name=Field(panel,"선택 묶음 이름",original?.Name??("선택군 "+(groups.Count+1)));var required=Field(panel,"이 묶음에서 선택할 과목 수",(original?.Required??1).ToString());var courses=new List<CourseEditor>();var group=new GroupEditor(original?.Id??Guid.NewGuid().ToString("N"),name,required,courses);groups.Add(group);
            void AddCourse(ElectiveCourse? course)
            {
                var entry=new StackPanel{Margin=new Thickness(0,8,0,8)};panel.Children.Add(entry);var courseName=Field(entry,"과목명",course?.Name??"");entry.Children.Add(Text("담당 교사 · 여러 명은 Ctrl 키로 선택"));var select=new ListBox{ItemsSource=teachers,SelectionMode=SelectionMode.Multiple,Height=72};entry.Children.Add(select);foreach(var teacher in teachers.Where(t=>course?.TeacherIds.Contains(t.Id)==true))select.SelectedItems.Add(teacher);
                var item=new CourseEditor(course?.Id??Guid.NewGuid().ToString("N"),courseName,select);courses.Add(item);Button(entry,"이 과목 제외",()=>{courses.Remove(item);panel.Children.Remove(entry);return Task.CompletedTask;});
            }
            if(original is null){AddCourse(null);AddCourse(null);}else foreach(var course in original.Courses)AddCourse(course);
            Button(panel,"과목 추가",()=>{AddCourse(null);return Task.CompletedTask;});Button(panel,"이 선택 묶음 제외",()=>{groups.Remove(group);groupPanel.Children.Remove(border);return Task.CompletedTask;});
        }
        if(round is null)AddGroup(null);else foreach(var group in round.Groups)AddGroup(group);Button(form,"선택 묶음 추가",()=>{AddGroup(null);return Task.CompletedTask;});
        ElectiveRoundRequest? pending=null;var client=Guid.NewGuid().ToString("N");
        Button(body,"초안 저장",async()=>
        {
            if(pending is null)
            {
                var choices=new List<ElectiveGroup>();foreach(var group in groups){if(!int.TryParse(group.Required.Text,out var count))throw new InvalidOperationException("선택 개수는 숫자로 입력하세요.");choices.Add(new(group.Id,group.Name.Text.Trim(),count,group.Courses.Select(c=>new ElectiveCourse(c.Id,c.Name.Text.Trim(),c.Teachers.SelectedItems.Cast<Choice>().Select(t=>t.Id).ToArray())).ToArray()));}
                pending=new(client,title.Text.Trim(),term.Text.Trim(),rooms.Where(r=>r.Value.IsChecked==true).Select(r=>r.Key).ToArray(),ParseTime(start.Text),ParseTime(end.Text),choices.ToArray(),round?.Revision??0);form.IsEnabled=false;
            }
            try{await api.Send<JsonElement>(HttpMethod.Post,"api/elective-management"+(round is null?"":"/"+round.Id),pending);await List();}
            catch(ApiRequestException e)when((int)e.StatusCode<500&&(int)e.StatusCode!=409){pending=null;form.IsEnabled=true;throw;}
        });
    }
    async Task Detail(string id)
    {
        var report=await api.Get<ElectiveReport>("api/elective-management/"+id);var round=report.Round;body.Children.Clear();Button(body,"회차 목록",List);Button(body,"현황 새로고침",()=>Detail(id));body.Children.Add(Text(round.Title,22));body.Children.Add(Text(round.Term+" · "+State(round.State)+" · 정원 제한 없음"));body.Children.Add(Text(Time(round.StartsAt)+" ~ "+Time(round.EndsAt)+" (한국 시각)"));
        foreach(var group in round.Groups){body.Children.Add(Text(group.Name+" · "+group.Required+"개 선택",16));foreach(var course in group.Courses)body.Children.Add(Text(course.Name+" · 신청 "+report.Students.Count(s=>s.Submitted&&s.CourseIds.Contains(course.Id))+"명"));}
        body.Children.Add(Text("대상 "+report.Students.Length+"명 · 신청 "+report.Students.Count(s=>s.Submitted)+"명 · 미신청 "+report.Students.Count(s=>!s.Submitted)+"명",17));
        if(report.ExcludedApplications>0)body.Children.Add(Text("전학·학급 이동·계정 중지 등으로 현재 대상에서 제외된 기존 신청 "+report.ExcludedApplications+"건은 확정 명부에 포함되지 않습니다."));
        if(round.State=="draft"){Button(body,"초안 편집",()=>Editor(round));Transition("신청 공개","open","기간과 대상 학급을 확인했나요? 공개하면 과목·선택 규칙을 편집할 수 없습니다.");}
        if(round.State=="open")Transition("신청 마감","close","지금 마감하면 학생의 신청·변경·취소가 중단됩니다. 마감할까요?");
        if(round.State=="closed"||round.State=="open"&&round.EndsAt<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())Transition("신청 결과를 분반 명부로 확정","finalize","현재 유효한 신청 전체를 정원 제한 없이 새 분반 명부로 확정합니다. 미신청자는 자동 배정하지 않습니다. 진행할까요?");
        if(round.State=="finalized")body.Children.Add(Text("분반 "+report.Sections.Length+"개를 확정했습니다. 담당 교사는 기존 학교 공지에서 해당 분반을 선택할 수 있습니다."));
        Button(body,"현재 신청 현황 CSV 저장",()=>{Export(report);return Task.CompletedTask;});var names=round.Groups.SelectMany(g=>g.Courses.Select(c=>(c.Id,Name:g.Name+" · "+c.Name))).ToDictionary(c=>c.Id,c=>c.Name);
        foreach(var student in report.Students)body.Children.Add(Text(student.ClassName+" · "+student.Name+" · "+(student.Submitted?string.Join(", ",student.CourseIds.Select(c=>names.GetValueOrDefault(c,"확인 필요"))):"미신청")));
        void Transition(string label,string action,string confirmation)
        {var request=new ElectiveTransition(Guid.NewGuid().ToString("N"),round.Revision,action);Button(body,label,async()=>{if(MessageBox.Show(this,confirmation,"수강신청",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;await api.Send<JsonElement>(HttpMethod.Post,"api/elective-management/"+id+"/state",request);await Detail(id);});}
    }
    static void Export(ElectiveReport report)
    {
        var picker=new SaveFileDialog{Filter="CSV 파일|*.csv",FileName="수강신청현황.csv"};if(picker.ShowDialog()!=true)return;
        static string Cell(string value){if(value.TrimStart().StartsWith('=')||value.TrimStart().StartsWith('+')||value.TrimStart().StartsWith('-')||value.TrimStart().StartsWith('@'))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
        var names=report.Round.Groups.SelectMany(g=>g.Courses.Select(c=>(c.Id,Name:g.Name+" · "+c.Name))).ToDictionary(c=>c.Id,c=>c.Name);var rows=new List<string>{"학급,이름,상태,선택과목"};rows.AddRange(report.Students.Select(s=>string.Join(",",new[]{s.ClassName,s.Name,s.Submitted?"신청":"미신청",string.Join(" / ",s.CourseIds.Select(c=>names.GetValueOrDefault(c,"확인 필요")))}.Select(Cell))));System.IO.File.WriteAllLines(picker.FileName,rows,new UTF8Encoding(true));
    }
    internal async Task<string[]> Verify(Action<Window,string> snapshot,string directory)
    {
        for(var n=0;n<150&&busy;n++)await Task.Delay(100);if(busy)throw new InvalidOperationException("수강신청 UI 로딩 시간 초과");
        await Run(()=>Editor(null));if(error.Text.Length>0)throw new InvalidOperationException(error.Text);
        static IEnumerable<T> Controls<T>(DependencyObject root)where T:DependencyObject
        {foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){if(child is T control)yield return control;foreach(var nested in Controls<T>(child))yield return nested;}}
        var fields=Controls<TextBox>(body).ToArray();if(fields.Length<8)throw new InvalidOperationException("수강신청 과목 입력 화면 누락");
        var title="UI 수강신청 "+Guid.NewGuid().ToString("N")[..8];fields[0].Text=title;fields[6].Text="UI 과목 1";fields[7].Text="UI 과목 2";
        var room=Controls<CheckBox>(body).First();room.IsChecked=true;foreach(var list in Controls<ListBox>(body))list.SelectedIndex=0;
        UpdateLayout();snapshot(this,System.IO.Path.Combine(directory,"elective-editor.png"));
        var save=body.Children.OfType<Button>().Single(b=>Equals(b.Content,"초안 저장"));save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        for(var n=0;n<150&&busy;n++)await Task.Delay(100);if(busy||error.Text.Length>0)throw new InvalidOperationException("수강신청 UI 저장 실패: "+error.Text);
        var created=(await api.Get<ElectiveRound[]>("api/elective-management")).Single(r=>r.Title==title);if(created.Groups.Single().Courses.Length!=2)throw new InvalidOperationException("수강신청 UI 저장 내용 불일치");
        await Run(()=>Detail(created.Id));if(error.Text.Length>0)throw new InvalidOperationException(error.Text);UpdateLayout();snapshot(this,System.IO.Path.Combine(directory,"elective-status.png"));
        return ["Native elective editor selects classes and teachers without capacity controls","Native draft save reaches the authorized server and preserves both courses","Native status renders current applicants and non-applicants"];
    }
}
