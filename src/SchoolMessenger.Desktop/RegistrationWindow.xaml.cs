using System.Text.Json;
using System.Windows;

namespace SchoolMessenger.Desktop;
public partial class RegistrationWindow : Window
{
    readonly Api api;
    bool sending;
    public RegistrationWindow(string address)
    {
        api = new Api(address); InitializeComponent();
        Closed += (_, _) => { PasswordBox.Clear(); ConfirmBox.Clear(); api.Dispose(); };
    }
    async void SubmitClick(object sender, RoutedEventArgs e)
    {
        if (sending) return;
        if (PasswordBox.Password != ConfirmBox.Password) { Feedback.Text = "비밀번호 확인이 일치하지 않습니다."; return; }
        sending = true; SubmitButton.IsEnabled = false;
        try
        {
            var response = await api.Send<JsonElement>(HttpMethod.Post, "api/register", new { username = UsernameBox.Text.Trim(), name = NameBox.Text.Trim(), department = DepartmentBox.Text.Trim(), password = PasswordBox.Password });
            PasswordBox.Clear(); ConfirmBox.Clear(); Feedback.Text = response.GetProperty("message").GetString();
            SubmitButton.Content = "신청 완료 · 창을 닫고 승인 후 로그인하세요";
        }
        catch (Exception error) { Feedback.Text = error.Message; SubmitButton.IsEnabled = true; }
        finally { sending = false; }
    }
}
