using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Desktop;

public sealed partial class OfficeWindow
{
    async Task StaffList()
    {
        body.Children.Clear();var title=Text("교직원·권한",22);title.FontWeight=FontWeights.Bold;body.Children.Add(title);body.Children.Add(Text("이름 · 부서 · 직책 · 좌석과 계정 권한을 관리합니다.",12));
        Button("교직원 등록 · 가입 승인 · 담당 권한",Administration);body.Children.OfType<Button>().Last().SetResourceReference(StyleProperty,"PrimaryButton");
        var users=await api.Get<JsonElement>("api/admin/users");
        var searchGrid=new Grid{Margin=new Thickness(0,16,0,16)};var search=new TextBox{MinHeight=38};System.Windows.Automation.AutomationProperties.SetName(search,"교직원 검색");search.ToolTip="이름 · 부서 · 로그인 아이디 검색";searchGrid.Children.Add(search);var hint=new TextBlock{Text="이름 · 부서 · 로그인 아이디 검색",IsHitTestVisible=false,Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};hint.SetResourceReference(TextBlock.ForegroundProperty,"Muted");searchGrid.Children.Add(hint);body.Children.Add(searchGrid);var list=new StackPanel();body.Children.Add(list);
        void Render()
        {
            list.Children.Clear();var query=search.Text.Trim();hint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
            foreach(var user in users.EnumerateArray())
            {
                var name=user.GetProperty("name").GetString()??"";var department=user.GetProperty("department").GetString()??"";var username=user.GetProperty("username").GetString()??"";
                if(query.Length>0&&!(name+" "+department+" "+username).Contains(query,StringComparison.CurrentCultureIgnoreCase))continue;
                var row=new DockPanel();var edit=new Button{Content="설정",MinWidth=80,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,0,0)};System.Windows.Automation.AutomationProperties.SetName(edit,name+" 계정 설정");edit.Click+=async(_,_)=>await Run(()=>StaffSettings(user));DockPanel.SetDock(edit,Dock.Right);row.Children.Add(edit);
                var labels=new StackPanel();var person=Text(name,16);person.FontWeight=FontWeights.SemiBold;labels.Children.Add(person);var position=user.TryGetProperty("position",out var p)?p.GetString():"";var meta=Text(string.Join(" · ",new[]{department,position,username,user.GetProperty("active").GetBoolean()?"활성":"중지"}.Where(s=>!string.IsNullOrWhiteSpace(s))),12);meta.SetResourceReference(TextBlock.ForegroundProperty,"Muted");labels.Children.Add(meta);row.Children.Add(labels);
                var card=new Border{Child=row,Padding=new Thickness(16,6,16,6),CornerRadius=new CornerRadius(14),Margin=new Thickness(0,0,0,12)};card.SetResourceReference(Border.BackgroundProperty,"Soft");list.Children.Add(card);
            }
            if(list.Children.Count==0)list.Children.Add(Text("일치하는 교직원이 없습니다."));
        }
        search.TextChanged+=(_,_)=>Render();Render();
    }
    Task StaffSettings(JsonElement user)
    {
        body.Children.Clear();Button("교직원 목록으로",StaffList);var id=user.GetProperty("id").GetString()!;
        var identifier=Field("교내 계정 ID · 외부 교사 연결에 사용",id);identifier.IsReadOnly=true;
        var name=Field("이름",user.GetProperty("name").GetString()!);var department=Field("부서",user.GetProperty("department").GetString()!);var position=Field("직책 · 표시용, 권한과 별도로 관리",user.TryGetProperty("position",out var title)?title.GetString()??"":"");position.MaxLength=50;
        var active=new CheckBox{Content="계정 사용 허용",IsChecked=user.GetProperty("active").GetBoolean()};var admin=new CheckBox{Content="시스템 관리자",IsChecked=user.GetProperty("isAdmin").GetBoolean()};var broadcast=new CheckBox{Content="학교 전체 공지 발송 허용",IsChecked=user.GetProperty("canBroadcast").GetBoolean()};body.Children.Add(active);body.Children.Add(admin);body.Children.Add(broadcast);
        Button("계정 상태 저장 · 기존 세션 회수",async()=>{await api.Send<JsonElement>(HttpMethod.Patch,"api/admin/users/"+id,new{name=name.Text,department=department.Text,position=position.Text,password="",isAdmin=admin.IsChecked==true,canBroadcast=broadcast.IsChecked==true,active=active.IsChecked==true});await api.RefreshSession();await StaffList();});
        Button("좌석 · 초기화 관리",()=>StaffSeat(user));
        return Task.CompletedTask;
    }
    Task StaffSeat(JsonElement user)
    {
        body.Children.Clear();Button("교직원 목록으로",StaffList);var id=user.GetProperty("id").GetString()!;
        body.Children.Add(Text(user.GetProperty("name").GetString()+" · "+user.GetProperty("username").GetString(),20));
        var ip=Field("좌석 IPv4 · 비우면 연결 해제",user.TryGetProperty("seatIp",out var seat)&&seat.ValueKind==JsonValueKind.String?seat.GetString()!:"");
        Button("좌석 변경 · 계정과 업무 기록 유지",async()=>{await api.Send<JsonElement>(HttpMethod.Post,"api/admin/staff/"+id+"/seat",new StaffSeatRequest(ip.Text));await StaffList();});
        body.Children.Add(Text("계정 초기화는 모든 기기의 세션을 회수합니다. 새 번호는 교사 본인에게 개별 전달하세요."));var login=Field("로그인 아이디 · 본인 이름, 동명이인은 이름.2",user.GetProperty("name").GetString()!);body.Children.Add(Text("이름 아이디와 초기 비밀번호 12345678로 초기화합니다. 새 활성화 번호 확인과 개인 비밀번호 설정이 필요합니다."));
        Button("계정 초기화 · 활성화 번호 재발급",async()=>
        {
            if(MessageBox.Show(InteractionOwner,"로그인 이름과 초기 비밀번호를 적용하고, 모든 기기의 세션을 회수한 뒤 활성화를 다시 요구할까요?","교직원 계정 초기화",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            var result=await api.Send<JsonElement>(HttpMethod.Post,"api/admin/staff/"+id+"/reset",new StaffAccountRequest(login.Text.Trim(),user.GetProperty("name").GetString()!,user.GetProperty("department").GetString()!,null,"12345678",UseSharedPassword:true));
            MessageBox.Show(InteractionOwner,"개별 전달할 활성화 번호:\n"+result.GetProperty("activationCode").GetString()+"\n유효 기간: 7일.","계정 초기화");await StaffList();
        });return Task.CompletedTask;
    }
    async Task Audit()
    {
        body.Children.Clear();Button("계정 관리로",Administration);body.Children.Add(Text("최근 운영 변경 기록 · 최대 200건",20));
        var rows=await api.Get<JsonElement>("api/admin/office/audit");foreach(var row in rows.EnumerateArray()){var time=DateTimeOffset.FromUnixTimeMilliseconds(row.GetProperty("createdAt").GetInt64()).ToOffset(TimeSpan.FromHours(9));body.Children.Add(Text(time.ToString("yyyy-MM-dd HH:mm")+" · "+row.GetProperty("actor").GetString()+" · "+row.GetProperty("action").GetString()+"\n대상: "+row.GetProperty("resourceId").GetString(),12));}
    }
}
