using System.Globalization;

namespace SchoolMessenger.Mobile;

public sealed partial class MessengerPage
{
    async Task Signup(string address)
    {
        var source=new MessengerApi(address,false);
        try
        {
            await source.Refresh();var classes=(await source.Get("api/registration/classes")).EnumerateArray().ToArray();
            var page=new ContentPage{Title="학생·학부모 회원가입",BackgroundColor=BackgroundColor};
            var stack=new VerticalStackLayout{Padding=24,Spacing=16};page.Content=new ScrollView{Content=stack};
            var role=new Picker{Title="회원 구분",ItemsSource=new[]{"학부모","학생"},SelectedIndex=0};
            var name=new Entry{Placeholder="이름",MaxLength=50};
            var username=new Entry{Placeholder="아이디 (3~32자)",MaxLength=32,Keyboard=Keyboard.Create(KeyboardFlags.None),IsTextPredictionEnabled=false};
            var password=new Entry{Placeholder="비밀번호 (12~128자)",IsPassword=true,MaxLength=128};
            var room=new Picker{Title="학급",ItemsSource=classes.Select(c=>c.GetProperty("name").GetString()!).ToArray(),IsVisible=false};
            var number=new Entry{Placeholder="출석 번호 (1~99)",Keyboard=Keyboard.Numeric,MaxLength=2,IsVisible=false};
            var help=Text("학부모는 임시회원으로 가입합니다. 로그인 후 설정에서 자녀 고유번호를 입력하세요.",14);
            var result=Text("",13);var join=new Button{Text="회원가입 신청",BackgroundColor=Blue,TextColor=Colors.White};
            role.SelectedIndexChanged+=(_,_)=>{room.IsVisible=number.IsVisible=role.SelectedIndex==1;help.Text=role.SelectedIndex==1?"학교가 학생 명부와 대조하여 승인한 뒤 로그인할 수 있습니다.":"학부모는 임시회원으로 가입합니다. 로그인 후 설정에서 자녀 고유번호를 입력하세요.";};
            foreach(var view in new View[]{role,name,username,password,room,number,help,join,result})stack.Add(view);
            var sending=false;
            join.Clicked+=async(_,_)=>
            {
                if(sending)return;sending=true;stack.IsEnabled=false;result.Text="";
                try
                {
                    var student=role.SelectedIndex==1;
                    if(student&&(room.SelectedIndex<0||!int.TryParse(number.Text,NumberStyles.None,CultureInfo.InvariantCulture,out _)))throw new InvalidOperationException("학생의 학급과 출석 번호를 입력하세요.");
                    var response=await source.Send("api/register",new MobileSignup(name.Text,username.Text,password.Text,student?"student":"parent",student?classes[room.SelectedIndex].GetProperty("id").GetString():null,student?int.Parse(number.Text!,CultureInfo.InvariantCulture):null));
                    password.Text="";await Navigation.PopAsync();feedback.Text=response.GetProperty("message").GetString();
                }
                catch(Exception e){password.Text="";result.Text=e.Message;}
                finally{sending=false;stack.IsEnabled=true;}
            };
            page.Disappearing+=(_,_)=>source.Dispose();await Navigation.PushAsync(page);
        }
        catch{source.Dispose();throw;}
    }

    async Task AccountSettings()
    {
        var source=api!;var version=epoch;var settings=await source.Get("api/settings");if(!Current(source,version))return;
        var parent=source.Session.GetProperty("user").GetProperty("role").GetString()=="parent";
        var page=new ContentPage{Title=parent?"설정 · 자녀 연결":"설정 · 내 고유번호",BackgroundColor=BackgroundColor};
        var stack=new VerticalStackLayout{Padding=24,Spacing=16};page.Content=new ScrollView{Content=stack};
        if(!parent)
        {
            stack.Add(Text("내 고유번호",24,true));
            stack.Add(new Entry{Text=settings.GetProperty("studentCode").GetString(),IsReadOnly=true,FontSize=26,IsTextPredictionEnabled=false});
            stack.Add(Text("학부모가 본인의 임시계정 설정에서 이 번호를 입력하면 자녀가 연결됩니다. 가족에게만 전달하세요. 영문 대소문자를 구분하며, 유출되면 학교 관리자에게 재발급을 요청하세요.",14));
        }
        else
        {
            var status=Text("",16,true);var children=new VerticalStackLayout{Spacing=8};
            async Task RefreshChildren()
            {
                var list=await source.Get("api/children");if(!Current(source,version))return;
                status.Text=list.GetArrayLength()==0?"임시회원 · 자녀 연결 필요":"정회원 · 연결된 자녀";children.Clear();
                foreach(var child in list.EnumerateArray())children.Add(Text(child.GetProperty("name").GetString()+" · "+child.GetProperty("className").GetString()));
            }
            await RefreshChildren();if(!Current(source,version))return;stack.Add(status);stack.Add(children);
            stack.Add(Text("재학 중인 자녀의 고유번호를 입력하세요. 번호가 맞으면 바로 정회원으로 전환됩니다. 자녀가 여러 명이면 각각 추가하세요.",14));
            var code=new Entry{Placeholder="자녀 고유번호 (8자리)",MaxLength=8,Keyboard=Keyboard.Create(KeyboardFlags.None),IsTextPredictionEnabled=false};
            var result=Text("",13);var link=new Button{Text="자녀 연결",BackgroundColor=Blue,TextColor=Colors.White};stack.Add(code);stack.Add(link);stack.Add(result);
            var sending=false;
            link.Clicked+=async(_,_)=>
            {
                if(sending)return;sending=true;stack.IsEnabled=false;result.Text="";
                try
                {
                    await source.Send("api/settings/children",new MobileFamilyCode(code.Text));code.Text="";await source.Refresh();await RefreshChildren();
                    if(Current(source,version))result.Text="자녀 연결 완료. 정회원으로 이용할 수 있습니다.";
                }
                catch(Exception e){result.Text=e.Message;}
                finally{sending=false;stack.IsEnabled=true;}
            };
        }
        if(Current(source,version))await Navigation.PushAsync(page);
    }
}
