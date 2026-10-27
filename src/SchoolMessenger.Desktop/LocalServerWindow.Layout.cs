using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SchoolMessenger.Desktop;

public sealed partial class LocalServerWindow
{
    static Border Surface(UIElement child,Thickness? padding=null)
    {
        var border=new Border{Child=child,Padding=padding??new Thickness(20),CornerRadius=new CornerRadius(14)};
        border.SetResourceReference(Border.BackgroundProperty,"Surface");return border;
    }
    static TextBlock Note(string value)
    {
        var note=Text(value,12);note.SetResourceReference(TextBlock.ForegroundProperty,"Muted");return note;
    }
    void BuildDashboard()
    {
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(224)});layout.ColumnDefinitions.Add(new ColumnDefinition());Content=layout;
        var side=new DockPanel{Margin=new Thickness(16,24,16,20)};
        var heading=new StackPanel();var title=Text("서버 관리",22);title.FontWeight=FontWeights.Bold;heading.Children.Add(title);heading.Children.Add(Note("이 컴퓨터에서 실행할 서버"));DockPanel.SetDock(heading,Dock.Top);side.Children.Add(heading);
        var bottom=new StackPanel();bottom.Children.Add(Note("시작 설정"));autoProgram.Content=new TextBlock{Text="Windows 로그인 시\n관리 프로그램 자동 실행",TextWrapping=TextWrapping.Wrap};autoProgram.VerticalContentAlignment=VerticalAlignment.Top;bottom.Children.Add(autoProgram);
        var guide=new Button{Content="운영 안내 열기",MinHeight=38};guide.Click+=(_,_)=>OpenGuide();bottom.Children.Add(guide);DockPanel.SetDock(bottom,Dock.Bottom);side.Children.Add(bottom);
        var navigation=new StackPanel{Margin=new Thickness(0,28,0,0)};side.Children.Add(navigation);layout.Children.Add(Surface(side,new Thickness(0)));
        var main=new DockPanel{Margin=new Thickness(28,24,28,16)};Grid.SetColumn(main,1);layout.Children.Add(main);
        var footer=Note("창을 닫으면 알림 영역에서 계속 실행됩니다.");footer.Margin=new Thickness(0,16,0,0);DockPanel.SetDock(footer,Dock.Bottom);main.Children.Add(footer);main.Children.Add(serverViews);
        foreach(var service in new[]{"office","announcements"})AddCard(navigation,service);
        SelectCard(cards.FirstOrDefault(c=>c.Host.Installed)??cards[0]);
    }
    void SelectCard(ServerCard card)
    {
        foreach(var c in cards)
        {
            c.View.Visibility=c==card?Visibility.Visible:Visibility.Collapsed;
            c.Navigation.SetResourceReference(Control.BackgroundProperty,c==card?"Pale":"Surface");
            c.Navigation.SetResourceReference(Control.ForegroundProperty,c==card?"Blue":"Ink");
            c.Navigation.BorderThickness=new Thickness(c==card?1:0);
            System.Windows.Automation.AutomationProperties.SetHelpText(c.Navigation,c==card?"선택된 서버":"이 서버의 실행 설정 보기");
        }
    }
    void AddCard(StackPanel navigation,string service)
    {
        var card=new ServerCard(service,new LocalServerProcess(root,service));cards.Add(card);
        var navContent=new StackPanel();var navTitle=Text(service=="office"?"교내 업무":"학생·학부모 소식",14);navTitle.FontWeight=FontWeights.SemiBold;navContent.Children.Add(navTitle);navContent.Children.Add(card.NavigationStatus);
        card.Navigation.Content=navContent;card.Navigation.Padding=new Thickness(12,10,12,10);card.Navigation.Margin=new Thickness(0,0,0,8);card.Navigation.Click+=(_,_)=>SelectCard(card);navigation.Children.Add(card.Navigation);
        System.Windows.Automation.AutomationProperties.SetName(card.Navigation,navTitle.Text+" 서버 선택");
        card.View.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});card.View.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});card.View.RowDefinitions.Add(new RowDefinition());serverViews.Children.Add(card.View);
        var heading=new StackPanel{Margin=new Thickness(0,0,0,22)};var title=Text(service=="office"?"교내 업무 서버":"학생·학부모 소식 서버",22);title.FontWeight=FontWeights.Bold;heading.Children.Add(title);heading.Children.Add(Note(service=="office"?"교직원 메신저와 교사용 앱의 업무를 연결합니다.":"학생·학부모 앱으로 학교의 공지와 소식을 전달합니다."));card.View.Children.Add(heading);
        var status=new StackPanel();card.Status.FontWeight=FontWeights.Bold;status.Children.Add(card.Status);card.Address.Margin=new Thickness(0,8,0,14);card.Address.SetResourceReference(TextBlock.ForegroundProperty,"Muted");status.Children.Add(card.Address);
        var actions=new WrapPanel();card.Start.SetResourceReference(StyleProperty,"PrimaryButton");card.Admin.Content="관리자 로그인";
        foreach(var button in new[]{card.Start,card.Stop,card.Admin}){button.MinWidth=110;button.MinHeight=38;button.Margin=new Thickness(0,0,10,0);actions.Children.Add(button);}status.Children.Add(actions);
        var statusCard=Surface(status);statusCard.Margin=new Thickness(0,0,0,16);Grid.SetRow(statusCard,1);card.View.Children.Add(statusCard);
        var controls=card.Controls;var settingsTitle=Text("실행 설정",16);settingsTitle.FontWeight=FontWeights.Bold;controls.Children.Add(settingsTitle);
        var mode=new Grid{Margin=new Thickness(0,12,0,0)};mode.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(100)});mode.ColumnDefinitions.Add(new ColumnDefinition());mode.Children.Add(Text("실행 모드"));Grid.SetColumn(card.Mode,1);mode.Children.Add(card.Mode);controls.Children.Add(mode);
        System.Windows.Automation.AutomationProperties.SetName(card.Mode,"서버 실행 모드");
        card.Data.Text=service=="office"&&Directory.Exists(@"C:\SchoolMessengerTrial\office")?@"C:\SchoolMessengerTrial\office":Path.Combine(directory,"trial",service);
        card.TrialFields.Children.Add(Note("가상 자료로 시험합니다. 외부 연결·파일 악성코드 검사는 사용하지 않습니다."));
        var passwords=new Grid{Margin=new Thickness(0,14,0,0)};passwords.ColumnDefinitions.Add(new ColumnDefinition());passwords.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});passwords.ColumnDefinitions.Add(new ColumnDefinition());
        var first=new StackPanel();first.Children.Add(Text("최초 관리자 비밀번호"));card.Password.MinHeight=38;first.Children.Add(card.Password);passwords.Children.Add(first);
        var second=new StackPanel();second.Children.Add(Text("비밀번호 확인"));card.Confirm.MinHeight=38;second.Children.Add(card.Confirm);Grid.SetColumn(second,2);passwords.Children.Add(second);
        System.Windows.Automation.AutomationProperties.SetName(card.Password,"최초 관리자 비밀번호");System.Windows.Automation.AutomationProperties.SetName(card.Confirm,"비밀번호 확인");
        card.PasswordFields.Children.Add(passwords);card.PasswordFields.Children.Add(Note("12~128자 · 서버를 처음 만들 때만 입력합니다."));card.TrialFields.Children.Add(card.PasswordFields);controls.Children.Add(card.TrialFields);
        card.ProductionFields.Margin=new Thickness(0,12,0,0);card.ProductionFields.Children.Add(Note("계정·HTTPS 인증서·백업을 준비한 운영 설정을 선택하세요."));card.ProductionFields.Children.Add(card.Config);
        var select=new Button{Content="운영 설정 파일 선택",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};select.Click+=(_,_)=>{var dialog=new OpenFileDialog{Filter="운영 설정 (*.json)|*.json",CheckFileExists=true};if(dialog.ShowDialog(this)==true){card.Config.Text=dialog.FileName;SaveSettings();Refresh();}};card.ProductionFields.Children.Add(select);controls.Children.Add(card.ProductionFields);
        card.Auto.Content=new TextBlock{Text="관리 프로그램을 열면 이 서버도 자동 시작",TextWrapping=TextWrapping.Wrap};card.Auto.Margin=new Thickness(0,18,0,12);card.Auto.ToolTip="시험 서버의 최초 설정을 완료한 후 선택할 수 있습니다.";controls.Children.Add(card.Auto);card.Auto.Click+=(_,_)=>SaveSettings();
        var path=new StackPanel();path.Children.Add(card.Data);path.Children.Add(Note("시험 데이터의 저장 위치입니다. 운영 저장 위치는 운영 설정에서 지정합니다."));var advanced=new Expander{Header="데이터 저장 위치",Content=path,Margin=new Thickness(0,6,0,8)};controls.Children.Add(advanced);
        card.Mode.SelectionChanged+=(_,_)=>{Refresh();SaveSettings();};card.Data.TextChanged+=(_,_)=>Refresh();
        card.Details.Content=card.Log;card.Log.FontFamily=new System.Windows.Media.FontFamily("Consolas");controls.Children.Add(card.Details);
        var settings=Surface(new ScrollViewer{Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Grid.SetRow(settings,2);card.View.Children.Add(settings);
        card.Start.Click+=async(_,_)=>await Start(card);card.Stop.Click+=async(_,_)=>await Stop(card);card.Admin.Click+=(_,_)=>OpenAdmin(card);
        // Production paths are governed by the selected operation configuration.
        card.Mode.SelectionChanged+=(_,_)=>advanced.Visibility=card.Mode.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;
    }
    void OpenGuide()
    {
        try
        {
            var path=File.Exists(Path.Combine(root,"서버 설치 안내.txt"))?Path.Combine(root,"서버 설치 안내.txt"):Path.Combine(root,"docs","server-installer.md");
            var open=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","notepad.exe")){UseShellExecute=false};open.ArgumentList.Add(path);Process.Start(open);
        }catch(Exception e){MessageBox.Show(this,e.Message,"운영 안내");}
    }
}
