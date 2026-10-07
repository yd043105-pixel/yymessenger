using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;
namespace SchoolMessenger.Desktop;
public partial class MailboxWindow : Window
{
    readonly MainWindow main;
    Api? Api => main.Api;
    readonly string? personId;
    readonly ObservableCollection<MessageItem> messages = new();
    string box;
    Detail? detail;
    bool refreshing, rendering;
    public MailboxWindow(MainWindow main, string box = "received", Person? person = null)
    {
        this.main = main; this.box = person is null ? box : "conversation"; personId = person?.Id;
        Owner = main; Resources = main.Resources; InitializeComponent();
        MessageList.ItemsSource = messages;
        Title = person is null ? "온라인 교무실 · 메시지함" : person.Name + " · 메시지 관리함";
        BoxTitle.Text = person is null ? box switch { "sent" => "보낸 메시지", "unread" => "미확인 메시지", _ => "받은 메시지" } : person.Name + " 관련 메시지"; UpdateTabs();
        Loaded += async (_, _) => await Reload();
    }
    public Task Reload() => Run(() => Refresh());
    public Task ShowMessage(string id) => Run(() => OpenDetail(id));
    async Task Refresh(bool more = false)
    {
        if (Api is null || refreshing) return;
        refreshing = true;
        try
        {
            var selected = detail?.Message.Id;
            var path = $"api/messages?box={box}&q={Uri.EscapeDataString(SearchBox.Text)}&offset={(more ? messages.Count : 0)}";
            if (FromDate.SelectedDate is { } from) path += $"&from={new DateTimeOffset(from).ToUnixTimeMilliseconds()}";
            if (ToDate.SelectedDate is { } to) path += $"&to={new DateTimeOffset(to.AddDays(1)).ToUnixTimeMilliseconds() - 1}";
            if (personId is not null) path += "&person=" + Uri.EscapeDataString(personId);
            var rows = await Api.Get<MessageItem[]>(path);
            rendering = true;
            if (!more) messages.Clear();
            foreach (var row in rows) if (!messages.Any(m => m.Id == row.Id)) messages.Add(row with { IsSent = row.SenderId == Api.Session!.User!.Id });
            MessageList.SelectedItem = messages.FirstOrDefault(m => m.Id == selected);
            rendering = false;
            ListHint.Text = messages.Count == 0 ? "메시지가 없습니다. 검색 조건도 확인하세요." : $"{messages.Count}개 표시 · 한 번에 100개 조회";
            if (selected is not null) await OpenDetail(selected, markRead: false);
            ConnectionLabel.Text = "첨부파일은 업로드 후 30일간 보관됩니다.";
            StatusLabel.Text = "";
        }
        finally { refreshing = false; rendering = false; }
    }
    async Task OpenDetail(string id, bool markRead = true)
    {
        if (Api is null) return;
        detail = await Api.Get<Detail>($"api/messages/{id}");
        if (markRead && detail.Recipients.Any(r => r.Id == Api.Session!.User!.Id && r.ReadAt is null))
        {
            await Api.Send<JsonElement>(HttpMethod.Post, $"api/messages/{id}/read");
            detail = await Api.Get<Detail>($"api/messages/{id}");
        }
        DetailTitle.Text = detail.Message.Title;
        DetailMeta.Text = $"{detail.Message.SenderName}   ·   {DateTimeOffset.FromUnixTimeMilliseconds(detail.Message.CreatedAt).ToLocalTime():yyyy.MM.dd HH:mm}   ·   수신 {detail.Recipients.Length}명";
        DetailBody.Text = detail.Message.Body;
        SubmissionButton.Visibility = detail.SubmissionRequest ? Visibility.Visible : Visibility.Collapsed;
        var index = messages.ToList().FindIndex(m => m.Id == id);
        if (index >= 0)
        {
            rendering = true;
            messages[index] = messages[index] with { ReadAt = detail.Recipients.FirstOrDefault(r => r.Id == Api.Session!.User!.Id)?.ReadAt, ReadCount = detail.Recipients.Count(r => r.ReadAt is not null) };
            MessageList.SelectedItem = messages[index];
            rendering = false;
        }
        await main.UpdateUnread();
        FileList.ItemsSource = detail.Attachments; ReceiptList.ItemsSource = detail.Recipients;
        EmptyHint.Visibility = Visibility.Collapsed; DetailPanel.Visibility = Visibility.Visible;
    }
    async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception error) { StatusLabel.Text = error.Message; }
    }
    async void SelectMessage(object sender, SelectionChangedEventArgs e)
    { if (!rendering && MessageList.SelectedItem is MessageItem item) await Run(() => OpenDetail(item.Id)); }
    async void ChangeBox(object sender, RoutedEventArgs e)
    {
        box = (string)((System.Windows.Controls.Button)sender).Tag; UpdateTabs();
        BoxTitle.Text = box switch { "unread" => "미확인 메시지", "sent" => "보낸 메시지", _ => "받은 메시지" };
        detail = null; DetailPanel.Visibility = Visibility.Collapsed; EmptyHint.Visibility = Visibility.Visible;
        await Run(() => Refresh());
    }
    void UpdateTabs()
    {
        foreach (var tab in new[] { ReceivedTab, UnreadTab, SentTab })
        {
            tab.Foreground = (System.Windows.Media.Brush)FindResource((string)tab.Tag == box ? "Blue" : "Muted");
            tab.BorderBrush = (string)tab.Tag == box ? (System.Windows.Media.Brush)FindResource("Blue") : System.Windows.Media.Brushes.Transparent;
        }
    }
    async void NewMessage(object sender, RoutedEventArgs e) => await main.WriteMessage();
    async void RefreshClick(object sender, RoutedEventArgs e) => await Run(() => Refresh());
    async void LoadMore(object sender, RoutedEventArgs e) => await Run(() => Refresh(true));
    async void SearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) await Run(() => Refresh()); }

    async void Reply(object sender, RoutedEventArgs e) => await main.WriteMessage(detail?.Message.SenderId, detail is null ? null : "답장: " + detail.Message.Title);
    void OpenSubmission(object sender, RoutedEventArgs e) { if (detail is not null) new WorkWindow(main, detail.SubmissionRequestId ?? detail.Message.Id).Show(); }
    async void Download(object sender, RoutedEventArgs e)
    {
        if (Api is null || ((System.Windows.Controls.Button)sender).Tag is not Attachment file) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = file.Name };
        if (dialog.ShowDialog(this) == true) await Run(async () => { await Api.Download(file.Id, dialog.FileName); StatusLabel.Text = "파일을 저장했습니다."; });
    }

}
