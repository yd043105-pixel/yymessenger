using System.Windows;
namespace SchoolMessenger.Desktop;
public partial class App : System.Windows.Application
{
    readonly System.Threading.Mutex running = new(false, @"Local\YeoyangSchoolMessenger.Running");
    protected override void OnExit(ExitEventArgs e) { running.Dispose(); base.OnExit(e); }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Appearance.Apply(Appearance.Selected, Appearance.Glass);
        SystemParameters.StaticPropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SystemParameters.HighContrast))
                Dispatcher.Invoke(() => Appearance.Apply(Appearance.Selected, Appearance.Glass));
        };
    }
    void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(Window.GetWindow((DependencyObject)sender));
    void MaximizeWindow(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow((DependencyObject)sender);
        if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
        else SystemCommands.MaximizeWindow(window);
    }
    void CloseWindow(object sender, RoutedEventArgs e) => Window.GetWindow((DependencyObject)sender).Close();
}
