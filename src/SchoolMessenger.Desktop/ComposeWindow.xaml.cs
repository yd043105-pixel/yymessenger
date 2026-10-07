using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;

namespace SchoolMessenger.Desktop;

public partial class ComposeWindow : Window
{
    readonly MainWindow main;
    readonly Person[] people;
    readonly ObservableCollection<SourceFile> files = new();
    readonly List<Attachment> uploaded = new();
    readonly string draftPath;
    readonly string server;
    Pending? pending;
    bool sending, sent;
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public ComposeWindow(MainWindow main, Person[] people, string? recipient, string? title, string[]? selectedIds = null)
    {
        this.main = main; this.people = people;
        Owner = main; Resources = main.Resources;
        InitializeComponent();
        SubmissionDate.SelectedDate = DateTime.Today.AddDays(7);
        draftPath = Path.Combine(main.LocalDirectory, $"draft-{main.Api!.Session!.User!.Id}.json");
        server = main.Api.Address.ToString();
        foreach (var person in people) { person.Selected = person.Id == recipient || selectedIds?.Contains(person.Id) == true; person.Favorite = main.Favorites.Contains(person.Id); }
        Department.ItemsSource = new[] { "선택 안 함" }.Concat(people.Select(p => p.Department).Distinct().Order()).ToArray();
        Department.SelectedIndex = 0;
        SendAll.Visibility = main.Api.Session.User.IsAdmin || main.Api.Session.User.CanBroadcast ? Visibility.Visible : Visibility.Collapsed;
        FilesList.ItemsSource = files; TitleBox.Text = title ?? "";
        if (File.Exists(draftPath))
        {
            try
            {
                var draft = JsonSerializer.Deserialize<Draft>(File.ReadAllText(draftPath), Json);
                if (draft is not null && draft.Server == main.Api.Address.ToString())
                {
                    TitleBox.Text = draft.Title; BodyBox.Text = draft.Body; pending = draft.Pending;
                    CollectFiles.IsChecked = draft.SubmissionDeadline is not null;
                    if (draft.SubmissionDeadline is { } due) SubmissionDate.SelectedDate = DateTimeOffset.FromUnixTimeMilliseconds(due).ToOffset(TimeSpan.FromHours(9)).Date;
                    foreach (var person in people) person.Selected = draft.RecipientIds.Contains(person.Id);
                    Department.SelectedItem = draft.Department ?? "선택 안 함"; SendAll.IsChecked = draft.All;
                    foreach (var file in draft.Files) if (File.Exists(file.Path)) files.Add(file);
                    uploaded.AddRange(draft.Uploaded);
                    if (pending is not null) { Freeze(); Feedback.Text = "전송 결과 미확인. 같은 메시지를 재시도합니다. 내용 변경은 완료 확인 후 가능합니다."; }
                }
            }
            catch (Exception error) when (error is IOException or JsonException) { Feedback.Text = "임시 저장을 읽지 못했습니다."; }
        }
        FilterPeople(this, new RoutedEventArgs());
    }
    void FilterPeople(object sender, RoutedEventArgs e)
    {
        if (PeopleList is null || PeopleSearch is null || FavoritesOnly is null) return;
        var text = PeopleSearch.Text.Trim();
        PeopleList.ItemsSource = people.Where(p => p.Label.Contains(text, StringComparison.OrdinalIgnoreCase) && (FavoritesOnly.IsChecked != true || p.Favorite))
            .OrderByDescending(p => p.Favorite).ThenBy(p => p.Department).ThenBy(p => p.Name).ToArray();
    }
    void AddFiles(object sender, RoutedEventArgs e)
        => ChooseFiles();
    public void ChooseFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "업무 자료|*.hwp;*.hwpx;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.csv;*.png;*.jpg;*.jpeg;*.zip" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }
    void AddPaths(string[] paths)
    {
        if (pending is not null || sending) return;
        foreach (var path in paths)
        {
            if (!File.Exists(path) || files.Any(f => f.Path == path)) continue;
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 104_857_600 || files.Count >= 10 || files.Sum(f => new FileInfo(f.Path).Length) + info.Length > 209_715_200)
            { Feedback.Text = "파일당 100MB, 합계 200MB, 최대 10개입니다."; break; }
            files.Add(new SourceFile(path));
        }
    }
    void DropFiles(object sender, System.Windows.DragEventArgs e)
    { if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths) AddPaths(paths); e.Handled = true; }
    void FilesDragOver(object sender, System.Windows.DragEventArgs e)
    { if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) { e.Effects = System.Windows.DragDropEffects.Copy; e.Handled = true; } }
    void RemoveFile(object sender, RoutedEventArgs e)
    { if (pending is null && FilesList.SelectedItem is SourceFile file) files.Remove(file); }
    void Freeze()
    {
        CollectFiles.IsEnabled = false; SubmissionDate.IsEnabled = false;
        TitleBox.IsEnabled = false; BodyBox.IsEnabled = false; PeopleList.IsEnabled = false; PeopleSearch.IsEnabled = false;
        Department.IsEnabled = false; SendAll.IsEnabled = false; AddButton.IsEnabled = false; RemoveButton.IsEnabled = false;
    }
    void Unfreeze()
    {
        CollectFiles.IsEnabled = true; SubmissionDate.IsEnabled = true;
        TitleBox.IsEnabled = true; BodyBox.IsEnabled = true; PeopleList.IsEnabled = true; PeopleSearch.IsEnabled = true;
        Department.IsEnabled = true; SendAll.IsEnabled = true; AddButton.IsEnabled = true; RemoveButton.IsEnabled = true;
    }
    async void Send(object sender, RoutedEventArgs e)
    {
        if (main.Api is null || sending) return;
        if (pending is null && CollectFiles.IsChecked == true && SubmissionDate.SelectedDate is null) { Feedback.Text = "제출 마감일을 선택하세요."; return; }
        if (pending is null && (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(BodyBox.Text) ||
            (!people.Any(p => p.Selected) && Department.SelectedIndex <= 0 && SendAll.IsChecked != true)))
        { Feedback.Text = "제목·본문·받는 사람을 확인하세요."; return; }
        if (pending is null && SendAll.IsChecked == true && System.Windows.MessageBox.Show(this, $"전체 교직원 {people.Length}명에게 보내시겠습니까?", "전체 발송 확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        sending = true; SendButton.IsEnabled = false; Freeze();
        try
        {
            if (pending is null)
            {
                // Previous partial uploads are discarded before starting a new upload attempt.
                foreach (var old in uploaded) await main.Api.Send<JsonElement>(HttpMethod.Delete, $"api/attachments/{old.Id}");
                uploaded.Clear();
                foreach (var file in files) { Feedback.Text = $"첨부 업로드 중: {file.Description}"; uploaded.Add(await main.Api.Upload(file.Path)); SaveDraft(); }
                pending = new Pending(Guid.NewGuid().ToString(), TitleBox.Text.Trim(), BodyBox.Text, people.Where(p => p.Selected).Select(p => p.Id).ToArray(), uploaded.Select(f => f.Id).ToArray(), SendAll.IsChecked == true, Department.SelectedIndex > 0 ? (string)Department.SelectedItem : null, Deadline());
                SaveDraft();
            }
            Feedback.Text = "서버 저장 확인 중…";
            await main.Api.Send<JsonElement>(HttpMethod.Post, "api/messages", pending);
            sent = true; File.Delete(draftPath); Close();
        }
        catch (Exception error)
        {
            Feedback.Text = error.Message + (pending is null ? "" : "\n같은 내용으로 다시 보내면 중복 저장되지 않습니다.");
            if (pending is null) Unfreeze();
            SaveDraft();
        }
        finally { sending = false; SendButton.IsEnabled = true; }
    }
    long? Deadline() => CollectFiles.IsChecked == true && SubmissionDate.SelectedDate is { } day ? new DateTimeOffset(DateTime.SpecifyKind(day.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Unspecified), TimeSpan.FromHours(9)).ToUnixTimeMilliseconds() : null;
    void SaveDraft()
    {
        var draft = new Draft(server, TitleBox.Text, BodyBox.Text, people.Where(p => p.Selected).Select(p => p.Id).ToArray(),
            Department.SelectedIndex > 0 ? (string)Department.SelectedItem : null, SendAll.IsChecked == true, files.ToArray(), uploaded.ToArray(), pending, Deadline());
        var temporary = draftPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(draft, Json)); File.Move(temporary, draftPath, true);
    }
    void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (sending && !sent) { e.Cancel = true; return; }
        main.Favorites = people.Where(p => p.Favorite).Select(p => p.Id).ToHashSet();
        if (!sent) SaveDraft();
    }
    void DiscardDraft(object sender, RoutedEventArgs e)
    {
        if (sending) return;
        var text = pending is null ? "임시 저장한 메시지를 삭제하시겠습니까?" : "이미 서버에 저장됐을 수 있습니다. 보낸 메시지에서 확인했습니까? 임시 저장을 삭제하면 새 메시지를 작성합니다.";
        if (System.Windows.MessageBox.Show(this, text, "임시 저장 삭제", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        pending = null; uploaded.Clear(); files.Clear(); TitleBox.Clear(); BodyBox.Clear();
        foreach (var person in people) person.Selected = false;
        Department.SelectedIndex = 0; SendAll.IsChecked = false; CollectFiles.IsChecked = false; Unfreeze();
        File.Delete(draftPath); Feedback.Text = "새 메시지를 작성하세요.";
        FilterPeople(this, new RoutedEventArgs());
    }
}
record SourceFile(string Path) { public string Description => System.IO.Path.GetFileName(Path); }
record Pending(string ClientId, string Title, string Body, string[] RecipientIds, string[] AttachmentIds, bool All, string? Department, long? SubmissionDeadline = null);
record Draft(string Server, string Title, string Body, string[] RecipientIds, string? Department, bool All, SourceFile[] Files, Attachment[] Uploaded, Pending? Pending, long? SubmissionDeadline = null);
