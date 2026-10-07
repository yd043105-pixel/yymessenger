using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;

namespace SchoolMessenger.Desktop;
public partial class MainWindow : Window
{
    public Api? Api { get; private set; }
    public string LocalDirectory { get; } = Environment.GetEnvironmentVariable("SCHOOL_UI_LOCALDIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolMessenger");
    public HashSet<string> Favorites { get; set; } = new();
    readonly HashSet<string> selectedPeople = new(), knownIncoming = new();
    readonly HashSet<string> expandedGroups = new();
    Person[] people = [];
    bool favoritesOnly, onlineOnly, onlineFirst = true, schoolExpanded = true, refreshing, exiting;
    int unreadCount, peopleRefreshVersion;
    HubConnection? hub;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };
    readonly System.Windows.Forms.NotifyIcon tray;
    readonly WindowsStartup startup;
    readonly SavedLogin savedLogin;
    bool startupReady, loginOptionsReady, signingIn;
    public MainWindow()
    {
        InitializeComponent();
        MinHeight = Math.Min(580, SystemParameters.WorkArea.Height - 30);
        Height = Math.Min(820, SystemParameters.WorkArea.Height - 30);
        Directory.CreateDirectory(LocalDirectory);
        savedLogin = new SavedLogin(LocalDirectory);
        startup = new WindowsStartup(Environment.GetCommandLineArgs().Contains("--verify-ui")
            ? @"Software\YeoyangSchoolMessenger\StartupTests\" + Path.GetFileName(LocalDirectory.TrimEnd(Path.DirectorySeparatorChar)) + Guid.NewGuid().ToString("N")
            : @"Software\Microsoft\Windows\CurrentVersion\Run");
        AutoStartCheck.IsChecked = AutoStartMenu.IsChecked = startup.Enabled;
        startupReady = true;
        try
        {
            var file = Path.Combine(LocalDirectory, "preferences.json");
            if (File.Exists(file) && JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file)) is { } preferences)
            { ServerAddress.Text = preferences.ServerAddress; UsernameBox.Text = preferences.Username; Quiet.IsChecked = preferences.Quiet; DoNotDisturb.IsChecked = preferences.DoNotDisturb; RememberLogin.IsChecked = preferences.AutoLogin; Favorites = preferences.Favorites.ToHashSet(); }
        }
        catch (Exception error) when (error is IOException or JsonException) { LoginError.Text = "설정을 읽지 못했습니다. 서버 주소를 확인하세요."; }
        loginOptionsReady = true;
        tray = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Text = "여양고 온라인 교무실", Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("교무실 열기", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(CloseApp)); tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow); tray.BalloonTipClicked += (_, _) => Dispatcher.Invoke(OpenNotification);
        timer.Tick += async (_, _) => await Run(async () =>
        {
            if (Workspace.Visibility != Visibility.Visible) { if (RememberLogin.IsChecked == true) await TryAutomaticLogin(); return; }
            if (Api is null || refreshing) return;
            await Api.RefreshSession();
            if (Api.Session?.User is null) { await Disconnect(); LoginError.Text = "로그인 만료. 다시 로그인하세요."; return; }
            await Refresh();
            if (hub?.State == HubConnectionState.Disconnected) await ConnectNotifications();
        });
        Loaded += async (_, _) =>
        {
            if (Environment.GetCommandLineArgs().Contains("--verify-ui")) { await VerifyUi(); return; }
            if (startup.Enabled) { try { startup.SetEnabled(true); } catch (Exception error) { LoginError.Text = "자동 실행 설정을 갱신하지 못했습니다: " + error.Message; } }
            if (RememberLogin.IsChecked == true) { await TryAutomaticLogin(); if (savedLogin.Exists) timer.Start(); }
        };
    }
    public void SavePreferences() => File.WriteAllText(Path.Combine(LocalDirectory, "preferences.json"), JsonSerializer.Serialize(new Preferences(ServerAddress.Text, UsernameBox.Text, Quiet.IsChecked, Favorites.ToArray(), DoNotDisturb.IsChecked, RememberLogin.IsChecked == true)));
    void RememberChanged(object sender, RoutedEventArgs e)
    {
        if (!loginOptionsReady) return;
        if (RememberLogin.IsChecked != true) savedLogin.Clear();
        SavePreferences();
    }
    void RegisterClick(object sender, RoutedEventArgs e)
    {
        try { new RegistrationWindow(ServerAddress.Text) { Owner = this }.ShowDialog(); }
        catch (Exception error) { LoginError.Text = error.Message; }
    }
    void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!startupReady) return;
        var enabled = sender is MenuItem menu ? menu.IsChecked : AutoStartCheck.IsChecked == true;
        startupReady = false;
        try { startup.SetEnabled(enabled); AutoStartCheck.IsChecked = AutoStartMenu.IsChecked = enabled; }
        catch (Exception error)
        {
            AutoStartCheck.IsChecked = AutoStartMenu.IsChecked = !enabled;
            LoginError.Text = StatusLabel.Text = "자동 실행 설정 실패: " + error.Message;
        }
        finally { startupReady = true; }
    }
    async void LoginClick(object sender, RoutedEventArgs e)
    {
        if (signingIn) return; signingIn = true;
        LoginButton.IsEnabled = false; LoginError.Text = "연결 중…";
        try { await SignIn(); } catch (Exception error) { LoginError.Text = error.Message; }
        finally { LoginButton.IsEnabled = true; signingIn = false; }
    }
    async Task SignIn()
    {
        if (hub is not null) await hub.DisposeAsync(); Api?.Dispose(); Api = new Api(ServerAddress.Text);
        await Api.Login(UsernameBox.Text.Trim(), PasswordBox.Password, RememberLogin.IsChecked == true); PasswordBox.Clear(); SavePreferences();
        if (RememberLogin.IsChecked == true) savedLogin.Save(Api); else savedLogin.Clear();
        await EnterWorkspace();
    }
    async Task EnterWorkspace()
    {
        var user = Api!.Session!.User!; IdentityLabel.Text = user.Name + " 선생님"; IdentityDepartment.Text = user.Department; AvatarInitial.Text = user.Name[..1];
        AdminButton.Visibility = user.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        Workspace.Visibility = Visibility.Visible; LoginPanel.Visibility = Visibility.Collapsed;
        knownIncoming.Clear(); foreach (var message in await Api.Get<MessageItem[]>("api/messages?box=received")) knownIncoming.Add(message.Id);
        await Refresh(); await ConnectNotifications(); await RefreshPeople(); timer.Start();
        remote = new RemoteClient(this);
        try { await remote.Connect(); } catch { StatusLabel.Text = "원격 지원 연결 대기 중"; }
    }
    async Task TryAutomaticLogin()
    {
        if (signingIn || !savedLogin.Exists || RememberLogin.IsChecked != true) return;
        signingIn = true; LoginButton.IsEnabled = false; LoginError.Text = "자동 로그인 중…";
        try
        {
            Api?.Dispose(); Api = new Api(ServerAddress.Text);
            if (!savedLogin.Restore(Api, UsernameBox.Text.Trim())) { LoginError.Text = "자동 로그인 기간이 끝났습니다. 다시 로그인하세요."; return; }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Api.RefreshSession(timeout.Token);
            if (Api.Session?.User is null) { savedLogin.Clear(); timer.Stop(); LoginError.Text = "계정이 변경되었거나 자동 로그인이 만료되었습니다. 다시 로그인하세요."; return; }
            await EnterWorkspace(); LoginError.Text = "";
        }
        catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or JsonException)
        { savedLogin.Clear(); timer.Stop(); LoginError.Text = "저장된 자동 로그인 정보를 읽지 못했습니다. 다시 로그인하세요."; }
        catch (Exception error) { LoginError.Text = "학교 서버 연결을 기다리는 중입니다. 30초 후 다시 시도합니다. " + error.Message; }
        finally { LoginButton.IsEnabled = true; signingIn = false; }
    }
    async Task ConnectNotifications()
    {
        if (Api is null) return;
        if (hub is not null) await hub.DisposeAsync();
        hub = new HubConnectionBuilder().WithUrl(new Uri(Api.Address, "hub"), o => o.Cookies = Api.Cookies).WithAutomaticReconnect().Build();
        hub.On<string>("NewMessage", async id => await Dispatcher.InvokeAsync(() => Run(async () => { if (knownIncoming.Add(id)) Notify(); await Refresh(); })).Task.Unwrap());
        hub.On<string>("ReadChanged", async _ => await Dispatcher.InvokeAsync(() => Run(RefreshWindows)).Task.Unwrap());
        hub.On("PresenceChanged", async () => await Dispatcher.InvokeAsync(() => Run(RefreshPeople)).Task.Unwrap());
        hub.On<string>("ChatChanged", id => Dispatcher.InvokeAsync(() => { ChatChanged?.Invoke(id); NotifyFeature("새 채팅을 확인하세요.", "chat"); }).Task);
        hub.On<string>("SurveyChanged", id => Dispatcher.InvokeAsync(() => { SurveyChanged?.Invoke(id); NotifyFeature("설문 변경 사항을 확인하세요.", "survey"); }).Task);
        hub.Reconnecting += _ => { Dispatcher.Invoke(() => ConnectionLabel.Text = "재연결 중 · 수신 확인 계속"); return Task.CompletedTask; };
        hub.Reconnected += _ => Dispatcher.InvokeAsync(() => Run(Refresh)).Task.Unwrap();
        hub.Closed += _ => { Dispatcher.Invoke(() => ConnectionLabel.Text = "알림 연결 끊김 · 재시도 중"); return Task.CompletedTask; };
        try { await hub.StartAsync(); ConnectionLabel.Text = "학교 서버 연결됨"; }
        catch { ConnectionLabel.Text = "실시간 알림 연결 실패 · 수신 확인 30초"; }
    }
    async Task Refresh()
    {
        if (Api is null || refreshing) return;
        refreshing = true;
        try
        {
            await RefreshPeople(); await UpdateUnread();
            var incoming = await Api.Get<MessageItem[]>("api/messages?box=received");
            if (incoming.Any(m => !knownIncoming.Contains(m.Id))) Notify();
            foreach (var message in incoming) knownIncoming.Add(message.Id);
            await RefreshWindows();
            ConnectionLabel.Text = hub?.State == HubConnectionState.Connected ? "학교 서버 연결됨" : "학교 서버 연결됨 · 수신 확인 30초";
        }
        finally { refreshing = false; }
    }
    async Task RefreshWindows()
    {
        foreach (var window in OwnedWindows.OfType<MailboxWindow>().ToArray()) await window.Reload();
        foreach (var window in OwnedWindows.OfType<ChatWindow>().ToArray()) await window.Refresh();
        foreach (var window in OwnedWindows.OfType<SurveyWindow>().ToArray()) await window.Reload();
        foreach (var window in OwnedWindows.OfType<WorkWindow>().ToArray()) await window.Reload();
        if (remote is not null) { try { await remote.Connect(); } catch { } }
    }
    public async Task UpdateUnread()
    {
        if (Api is null) return;
        var unread = await Api.Get<MessageItem[]>("api/messages?box=unread");
        unreadCount = unread.Length; UpdateRosterHint();
    }
    async Task RefreshPeople()
    {
        var api = Api; if (api is null) return;
        var version = ++peopleRefreshVersion;
        var updated = await api.Get<Person[]>("api/users");
        if (api != Api || version != peopleRefreshVersion) return;
        people = updated;
        selectedPeople.IntersectWith(people.Select(p => p.Id)); RenderTree(); UpdateRosterHint();
    }
    void UpdateRosterHint()
    {
        RosterHint.Text = $"접속 {people.Count(p => p.Online)} / {people.Length}명 · 미확인 {unreadCount}{(unreadCount == 100 ? "+" : "")}개";
        AllFilter.Content = $"전체 {people.Length}"; OnlineFilter.Content = $"접속 중 {people.Count(p => p.Online)}";
    }
    void UpdateSelectionHint() => SelectionHint.Text = $"선택 {selectedPeople.Count}명";
    void RenderTree()
    {
        if (OrganizationTree is null || PeopleSearch is null) return;
        var selected = (OrganizationTree.SelectedItem as TreeViewItem)?.Tag as Person;
        void Remember(ItemsControl parent) { foreach (var row in parent.Items.OfType<TreeViewItem>()) { if (row.Tag is string key) { if (key == "school") schoolExpanded = row.IsExpanded; else if (row.IsExpanded) expandedGroups.Add(key); else expandedGroups.Remove(key); } Remember(row); } }
        Remember(OrganizationTree); bool first = OrganizationTree.Items.Count == 0;
        OrganizationTree.Items.Clear();
        var filtered = people.Where(p => p.Label.Contains(PeopleSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase) && (!favoritesOnly || Favorites.Contains(p.Id)) && (!onlineOnly || p.Online)).ToArray();
        var school = GroupRow("여양고등학교", "school", filtered.Select(p => p.Id).ToArray(), schoolExpanded || PeopleSearch.Text.Length > 0); OrganizationTree.Items.Add(school);
        if (filtered.Length == 0) school.Items.Add(new TreeViewItem { Header = favoritesOnly ? "즐겨찾기를 추가하세요." : "검색 결과가 없습니다.", IsEnabled = false });
        foreach (var group in filtered.GroupBy(p => p.Department).OrderBy(g => g.Key))
        {
            var row = GroupRow(group.Key, group.Key, group.Select(p => p.Id).ToArray(), first || expandedGroups.Contains(group.Key) || PeopleSearch.Text.Length > 0);
            school.Items.Add(row);
            foreach (var person in group.OrderByDescending(p => onlineFirst && p.Online).ThenBy(p => p.Name))
            {
                var check = new System.Windows.Controls.CheckBox { IsChecked = selectedPeople.Contains(person.Id), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
                check.Checked += (_, _) => { selectedPeople.Add(person.Id); UpdateSelectionHint(); }; check.Unchecked += (_, _) => { selectedPeople.Remove(person.Id); UpdateSelectionHint(); };
                var panel = new Grid { MinHeight = 34 };
                panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                panel.Children.Add(check);
                var avatar = new Grid { Width = 27, Height = 27, Margin = new Thickness(5, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                avatar.Children.Add(new System.Windows.Shapes.Ellipse { Fill = new SolidColorBrush(Color.FromRgb(231, 238, 249)) });
                avatar.Children.Add(new TextBlock { Text = person.Name[..1], FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(68, 98, 150)), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                avatar.Children.Add(new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = person.Online ? new SolidColorBrush(Color.FromRgb(33, 180, 137)) : new SolidColorBrush(Color.FromRgb(173, 183, 199)), Stroke = Brushes.White, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
                Grid.SetColumn(avatar, 1); panel.Children.Add(avatar);
                var name = new TextBlock { Text = person.Name + (Favorites.Contains(person.Id) ? " ★" : ""), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Ink"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(name, 2); panel.Children.Add(name);
                var badge = new Border { Background = person.Online ? new SolidColorBrush(Color.FromRgb(231, 249, 241)) : new SolidColorBrush(Color.FromRgb(240, 242, 245)), CornerRadius = new CornerRadius(5), Padding = new Thickness(5, 3, 5, 3), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = person.Online ? "접속" : "로그아웃", FontSize = 9, Foreground = person.Online ? new SolidColorBrush(Color.FromRgb(21, 127, 97)) : (Brush)FindResource("Muted") } };
                Grid.SetColumn(badge, 3); panel.Children.Add(badge);
                var contact = new TreeViewItem { Header = panel, Tag = person, ToolTip = person.Department + " · " + person.Presence, ContextMenu = PersonMenu(person) };
                contact.PreviewMouseRightButtonDown += (_, _) => contact.IsSelected = true;
                contact.MouseDoubleClick += async (_, e) => { if (e.ChangedButton != MouseButton.Left) return; e.Handled = true; await WriteMessage(person.Id); };
                row.Items.Add(contact); if (selected?.Id == person.Id) contact.IsSelected = true;
            }
        }
        UpdateSelectionHint();
    }
    TreeViewItem GroupRow(string label, string key, string[] ids, bool expanded)
    {
        var check = new System.Windows.Controls.CheckBox { Content = label + (key == "school" ? "" : $"  · {ids.Length}명"), FontSize = 11, FontWeight = FontWeights.SemiBold, IsChecked = ids.Length > 0 && ids.All(selectedPeople.Contains), Margin = new Thickness(0, 2, 0, 2) };
        check.Checked += (_, _) => { foreach (var id in ids) selectedPeople.Add(id); RenderTree(); };
        check.Unchecked += (_, _) => { foreach (var id in ids) selectedPeople.Remove(id); RenderTree(); };
        return new TreeViewItem { Header = check, Tag = key, IsExpanded = expanded };
    }
    internal ContextMenu PersonMenu(Person person)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = person.Online ? "접속 중" : "로그아웃 상태", IsEnabled = false });
        menu.Items.Add(new Separator());
        void Add(string text, RoutedEventHandler action) { var key = text.StartsWith("메시지 보내기") ? "MailIcon" : text.StartsWith("파일") ? "FileIcon" : text.StartsWith("메시지 관리") ? "HistoryIcon" : text.StartsWith("사용자") ? "UserIcon" : "StarIcon"; var item = new MenuItem { Header = text, Icon = new System.Windows.Shapes.Path { Data = (Geometry)FindResource(key), Stroke = (Brush)FindResource("Blue"), StrokeThickness = 1.5, Stretch = Stretch.Uniform, Width = 15, Height = 15 } }; item.Click += action; menu.Items.Add(item); }
        Add("메시지 보내기(_M)", async (_, _) => await WriteMessage(person.Id));
        Add("파일 보내기(_F)", async (_, _) => await WriteMessage(person.Id, filesOnly: true));
        Add("채팅하기(_C)", (_, _) => OpenChat([person.Id]));
        Add("설문 보내기(_V)", (_, _) => OpenSurvey([person.Id]));
        if (person.Online && person.Id != Api?.Session?.User?.Id)
        {
            Add("원격 지원하기", async (_, _) => await Run(() => RequestRemote(person.Id, false)));
            Add("원격 지원받기", async (_, _) => await Run(() => RequestRemote(person.Id, true)));
        }
        menu.Items.Add(new Separator());
        Add("메시지 관리함(_H)", (_, _) => OpenMailbox("conversation", person));
        Add("사용자 정보보기(_I)", (_, _) => System.Windows.MessageBox.Show(this, $"이름: {person.Name}\n부서: {person.Department}\n상태: {person.Presence}", "사용자 정보", MessageBoxButton.OK, MessageBoxImage.Information));
        menu.Items.Add(new Separator());
        Add(Favorites.Contains(person.Id) ? "즐겨찾기 해제" : "즐겨찾기 추가", (_, _) => { if (!Favorites.Add(person.Id)) Favorites.Remove(person.Id); SavePreferences(); RenderTree(); });
        return menu;
    }
    async Task Run(Func<Task> action) { try { await action(); } catch (Exception error) { StatusLabel.Text = error.Message; } }
    void SearchPeople(object sender, TextChangedEventArgs e) => RenderTree();
    void ToggleSort(object sender, RoutedEventArgs e) { onlineFirst = !onlineFirst; SortButton.Content = onlineFirst ? "이름순 ↕" : "접속순 ↕"; SortButton.ToolTip = onlineFirst ? "현재 접속자 우선 · 클릭하면 이름순" : "현재 이름순 · 클릭하면 접속자 우선"; RenderTree(); }
    void FilterAll(object sender, RoutedEventArgs e) { onlineOnly = false; UpdateFilters(); }
    void FilterOnline(object sender, RoutedEventArgs e) { onlineOnly = true; UpdateFilters(); }
    void UpdateFilters()
    {
        AllFilter.Background = (Brush)FindResource(onlineOnly ? "Soft" : "Pale"); AllFilter.Foreground = (Brush)FindResource(onlineOnly ? "Muted" : "Blue");
        OnlineFilter.Background = (Brush)FindResource(onlineOnly ? "Pale" : "Soft"); OnlineFilter.Foreground = (Brush)FindResource(onlineOnly ? "Blue" : "Muted"); RenderTree();
    }
    void UpdateNavigation()
    {
        OrganizationButton.Background = favoritesOnly ? Brushes.Transparent : (Brush)FindResource("Blue"); FavoritesButton.Background = favoritesOnly ? (Brush)FindResource("Blue") : Brushes.Transparent;
    }
    void ShowOrganization(object sender, RoutedEventArgs e) { favoritesOnly = false; PeopleSearch.Clear(); UpdateNavigation(); RenderTree(); }
    void ToggleFavorites(object sender, RoutedEventArgs e) { favoritesOnly = !favoritesOnly; UpdateNavigation(); RenderTree(); }
    async void Compose(object sender, RoutedEventArgs e) => await WriteMessage();
    async void SendFilesClick(object sender, RoutedEventArgs e) => await WriteMessage(filesOnly: true);
    public async Task WriteMessage(string? recipient = null, string? title = null, bool filesOnly = false)
    {
        await Run(async () =>
        {
            if (Api is null) return;
            var selected = recipient is null ? selectedPeople.ToArray() : new[] { recipient };
            if (selected.Length == 0 && (OrganizationTree.SelectedItem as TreeViewItem)?.Tag is Person person) selected = [person.Id];
            var contacts = await Api.Get<Person[]>("api/users");
            var dialog = new ComposeWindow(this, contacts, recipient, title ?? (filesOnly ? "파일 전달" : null), selected);
            if (filesOnly) { dialog.BodyBox.Text = "첨부파일을 전달합니다."; dialog.Loaded += (_, _) => dialog.ChooseFiles(); }
            dialog.ShowDialog(); SavePreferences(); await Refresh();
        });
    }
    void OpenMailboxClick(object sender, RoutedEventArgs e) => OpenMailbox((string)((System.Windows.Controls.Button)sender).Tag);
    internal MailboxWindow OpenMailbox(string box, Person? person = null)
    { var window = new MailboxWindow(this, box, person); window.Show(); return window; }
    void OpenSettings(object sender, RoutedEventArgs e) { var button = (System.Windows.Controls.Button)sender; button.ContextMenu.PlacementTarget = button; button.ContextMenu.IsOpen = true; }
    void PasswordKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) LoginClick(sender, e); }
    void OpenAdmin(object sender, RoutedEventArgs e) { if (Api is not null) Process.Start(new ProcessStartInfo(new Uri(Api.Address, "/").ToString()) { UseShellExecute = true }); }
    async void ChangePassword(object sender, RoutedEventArgs e)
    {
        if (Api is null) return;
        var old = new System.Windows.Controls.PasswordBox { Margin = new Thickness(0, 5, 0, 14) };
        var next = new System.Windows.Controls.PasswordBox { Margin = new Thickness(0, 5, 0, 14) };
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(new TextBlock { Text = "현재 비밀번호" }); panel.Children.Add(old);
        panel.Children.Add(new TextBlock { Text = "새 비밀번호 · 12~128자" }); panel.Children.Add(next);
        var save = new System.Windows.Controls.Button { Content = "변경 후 다시 로그인" }; panel.Children.Add(save);
        var dialog = new Window { Title = "비밀번호 변경", Owner = this, Width = 350, Height = 300, Content = panel, Resources = Resources, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        save.Click += async (_, _) => { save.IsEnabled = false; try { await Api.Send<JsonElement>(HttpMethod.Post, "api/password", new { currentPassword = old.Password, newPassword = next.Password }); dialog.Close(); await Disconnect(); } catch (Exception error) { System.Windows.MessageBox.Show(dialog, error.Message); } finally { save.IsEnabled = true; } };
        dialog.ShowDialog();
    }
    async void Logout(object sender, RoutedEventArgs e)
        => await SignOut();
    async Task SignOut()
    { try { if (Api is not null) await Api.Send<JsonElement>(HttpMethod.Post, "api/logout"); } catch (Exception error) { LoginError.Text = error.Message; } finally { RememberLogin.IsChecked = false; await Disconnect(); } }
    async Task Disconnect()
    {
        savedLogin.Clear();
        if (remote is not null) { await remote.DisposeAsync(); remote = null; }
        timer.Stop(); foreach (var window in OwnedWindows.Cast<Window>().ToArray()) window.Close();
        if (hub is not null) { await hub.DisposeAsync(); hub = null; } Api?.Dispose(); Api = null;
        OrganizationTree.Items.Clear(); people = []; selectedPeople.Clear(); Workspace.Visibility = Visibility.Collapsed; LoginPanel.Visibility = Visibility.Visible;
    }
    void Notify() { if (DoNotDisturb.IsChecked) return; notificationTarget = "message"; tray.ShowBalloonTip(5000, "온라인 교무실", "새 업무 메시지가 도착했습니다.", System.Windows.Forms.ToolTipIcon.Info); if (!Quiet.IsChecked) System.Media.SystemSounds.Asterisk.Play(); }
    void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    void WindowClosing(object? sender, CancelEventArgs e)
    { if (!exiting) { e.Cancel = true; Hide(); } else { timer.Stop(); tray.Dispose(); SavePreferences(); } }
    async void CloseApp() { if (remote is not null) { await remote.DisposeAsync(); remote = null; } exiting = true; Close(); }
    void ExitApp(object sender, RoutedEventArgs e) => CloseApp();
    static void SavePreview(Window window, string path)
    {
        window.UpdateLayout(); var content = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
        var height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            draw.DrawRectangle(new VisualBrush(content), null, new Rect(content.Margin.Left, content.Margin.Top, content.ActualWidth, content.ActualHeight));
        }
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); encoder.Save(output);
    }
    async Task VerifyUi()
    {
        try
        {
            try { startup.SetEnabled(true); if (!startup.Enabled || !WindowsStartup.Command.EndsWith(" --startup", StringComparison.Ordinal)) throw new InvalidOperationException("자동 실행 등록 실패"); }
            finally { startup.SetEnabled(false); }
            if (startup.Enabled) throw new InvalidOperationException("자동 실행 해제 실패");
            ServerAddress.Text = Environment.GetEnvironmentVariable("SCHOOL_UI_SERVER") ?? ServerAddress.Text;
            UsernameBox.Text = Environment.GetEnvironmentVariable("SCHOOL_UI_USER") ?? "admin";
            RememberLogin.IsChecked = true;
            PasswordBox.Password = Environment.GetEnvironmentVariable("SCHOOL_UI_PASSWORD") ?? ""; await SignIn();
            var saved = File.ReadAllBytes(savedLogin.FilePath);
            if (System.Text.Encoding.UTF8.GetString(saved).Contains(Environment.GetEnvironmentVariable("SCHOOL_UI_PASSWORD")!)) throw new InvalidOperationException("비밀번호 저장 금지 검증 실패");
            var originalUser = Api!.Session!.User!.Id;
            await Disconnect(); File.WriteAllBytes(savedLogin.FilePath, saved); await TryAutomaticLogin();
            if (Api?.Session?.User?.Id != originalUser || Workspace.Visibility != Visibility.Visible || PasswordBox.Password.Length != 0) throw new InvalidOperationException("암호화 정보로 자동 로그인 복원 실패");
            if (Width > 460 || people.Length == 0) throw new InvalidOperationException("조직도 크기 또는 교직원 목록 오류");
            Person[] VisibleContacts()
            {
                var found = new List<Person>();
                void Visit(ItemsControl parent) { foreach (var row in parent.Items.OfType<TreeViewItem>()) { if (row.Tag is Person contact) found.Add(contact); Visit(row); } }
                Visit(OrganizationTree); return found.ToArray();
            }
            var me = Api!.Session!.User!;
            for (var i = 0; i < 30 && !people.Any(p => p.Id == me.Id && p.Online); i++) { await Task.Delay(50); await RefreshPeople(); }
            PeopleSearch.Text = me.Name;
            if (!VisibleContacts().Any(p => p.Id == me.Id) || VisibleContacts().Any(p => !p.Label.Contains(me.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("조직도 검색 실패");
            PeopleSearch.Clear(); OnlineFilter.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (!VisibleContacts().Any() || VisibleContacts().Any(p => !p.Online)) throw new InvalidOperationException("접속 중 필터 실패");
            AllFilter.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (VisibleContacts().Length != people.Length) throw new InvalidOperationException("전체 교직원 필터 실패");
            if (OrganizationTree.Items.OfType<TreeViewItem>().SelectMany(r => r.Items.OfType<TreeViewItem>()).Any(r => !r.IsExpanded)) throw new InvalidOperationException("필터 변경 시 부서 펼침 상태 유지 실패");
            if (System.Windows.Shell.WindowChrome.GetWindowChrome(this)?.CaptionHeight != 44) throw new InvalidOperationException("제목줄 적용 실패");
            var output = Environment.GetEnvironmentVariable("SCHOOL_UI_OUTPUT") ?? "artifacts/client-preview.png";
            var directory = Path.GetDirectoryName(Path.GetFullPath(output))!; Directory.CreateDirectory(directory); SavePreview(this, output);
            foreach (var state in new[] { false, true })
            {
                var menu = PersonMenu(people[0] with { Online = state });
                var headers = menu.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToArray();
                if (headers[0] != (state ? "접속 중" : "로그아웃 상태") || headers.Any(h => h?.Contains("일정") == true || h?.Contains("알림톡") == true)) throw new InvalidOperationException("접속 상태별 메뉴 검증 실패");
                menu.PlacementTarget = OrganizationTree; menu.IsOpen = true;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var border = menu.Items.Count > 0 ? (Visual)menu : menu;
                var menuBitmap = new RenderTargetBitmap((int)menu.ActualWidth, (int)menu.ActualHeight, 96, 96, PixelFormats.Pbgra32); menuBitmap.Render(border);
                var menuEncoder = new PngBitmapEncoder(); menuEncoder.Frames.Add(BitmapFrame.Create(menuBitmap));
                using (var file = File.Create(Path.Combine(directory, state ? "online-menu.png" : "offline-menu.png"))) menuEncoder.Save(file);
                menu.IsOpen = false;
            }
            var inbox = OpenMailbox("received"); await inbox.Reload();
            var messages = await Api!.Get<MessageItem[]>("api/messages?box=received");
            if (messages.Length == 0) throw new InvalidOperationException("메시지 검증 데이터 없음");
            await inbox.ShowMessage(messages[0].Id); SavePreview(inbox, Path.Combine(directory, "mailbox-preview.png"));
            ((System.Windows.Controls.Button)inbox.Template.FindName("CloseAction", inbox)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            for (var i = 0; i < 20 && inbox.IsVisible; i++) await Task.Delay(25);
            if (inbox.IsVisible) throw new InvalidOperationException("제목줄 닫기 버튼 실패");
            var compose = new ComposeWindow(this, people, Api.Session!.User!.Id, "Windows 앱 전송 검증"); compose.Show();
            compose.BodyBox.Text = "교사용 프로그램의 작성 창에서 실제로 전송한 검증 메시지입니다."; SavePreview(compose, Path.Combine(directory, "compose-preview.png"));
            compose.SendButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            for (var i = 0; i < 100 && compose.IsVisible; i++) await Task.Delay(100);
            if (compose.IsVisible) throw new InvalidOperationException("작성 창 전송 실패: " + compose.Feedback.Text);
            if (!(await Api.Get<MessageItem[]>("api/messages?box=sent")).Any(m => m.Title == "Windows 앱 전송 검증")) throw new InvalidOperationException("전송 기록 없음");
            var registration = new RegistrationWindow(ServerAddress.Text) { Owner = this }; registration.Show();
            registration.UsernameBox.Text = "native.teacher"; registration.NameBox.Text = "가입신청교사"; registration.DepartmentBox.Text = "교무기획부";
            registration.PasswordBox.Password = registration.ConfirmBox.Password = Environment.GetEnvironmentVariable("SCHOOL_UI_PASSWORD")!;
            registration.SubmitButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            for (var i = 0; i < 50 && !registration.SubmitButton.Content.ToString()!.StartsWith("신청 완료", StringComparison.Ordinal); i++) await Task.Delay(100);
            if (!registration.SubmitButton.Content.ToString()!.StartsWith("신청 완료", StringComparison.Ordinal)) throw new InvalidOperationException("Windows 계정 신청 실패: " + registration.Feedback.Text);
            SavePreview(registration, Path.Combine(directory, "registration-preview.png")); registration.Close();
            await VerifyFeatureUi(directory);
            await SignOut();
            if (savedLogin.Exists || RememberLogin.IsChecked == true) throw new InvalidOperationException("로그아웃 시 자동 로그인 삭제 실패");
            SavePreview(this, Path.Combine(directory, "login-preview.png"));
            exiting = true; System.Windows.Application.Current.Shutdown(0);
        }
        catch (Exception error) { Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/ui-error.txt", error.ToString()); exiting = true; System.Windows.Application.Current.Shutdown(1); }
    }
}
record Preferences(string ServerAddress, string Username, bool Quiet, string[] Favorites, bool DoNotDisturb = false, bool AutoLogin = false);
