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
        body.Children.Clear();Button("관리 목록으로",Administration);var users=await api.Get<JsonElement>("api/admin/users");
        foreach(var user in users.EnumerateArray())Button(user.GetProperty("name").GetString()+" · "+user.GetProperty("username").GetString()+" · "+(user.GetProperty("active").GetBoolean()?"활성":"중지"),()=>StaffSettings(user));
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
        body.Children.Add(Text("계정 초기화는 모든 기기의 세션을 회수합니다. 새 번호는 교사 본인에게 개별 전달하세요."));body.Children.Add(Text("공통 임시 비밀번호 · 12~128자"));var password=new PasswordBox();body.Children.Add(password);
        Button("계정 초기화 · 활성화 번호 재발급",async()=>
        {
            if(MessageBox.Show(this,"현재 로그인 세션을 회수하고 개인 비밀번호 재설정을 요구할까요?","교직원 계정 초기화",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            JsonElement result;try{result=await api.Send<JsonElement>(HttpMethod.Post,"api/admin/staff/"+id+"/reset",new StaffAccountRequest(user.GetProperty("username").GetString()!,user.GetProperty("name").GetString()!,user.GetProperty("department").GetString()!,null,password.Password));}finally{password.Clear();}
            MessageBox.Show(this,"개별 전달할 활성화 번호:\n"+result.GetProperty("activationCode").GetString()+"\n유효 기간: 7일.","계정 초기화");await StaffList();
        });return Task.CompletedTask;
    }
    async Task Audit()
    {
        body.Children.Clear();Button("계정 관리로",Administration);body.Children.Add(Text("최근 운영 변경 기록 · 최대 200건",20));
        var rows=await api.Get<JsonElement>("api/admin/office/audit");foreach(var row in rows.EnumerateArray()){var time=DateTimeOffset.FromUnixTimeMilliseconds(row.GetProperty("createdAt").GetInt64()).ToOffset(TimeSpan.FromHours(9));body.Children.Add(Text(time.ToString("yyyy-MM-dd HH:mm")+" · "+row.GetProperty("actor").GetString()+" · "+row.GetProperty("action").GetString()+"\n대상: "+row.GetProperty("resourceId").GetString(),12));}
    }
}
