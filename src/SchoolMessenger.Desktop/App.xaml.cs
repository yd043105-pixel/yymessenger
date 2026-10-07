using System.Windows;
namespace SchoolMessenger.Desktop;
public partial class App : System.Windows.Application
{
    void MinimizeWindow(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(Window.GetWindow((DependencyObject)sender));
    void MaximizeWindow(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow((DependencyObject)sender);
        if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
        else SystemCommands.MaximizeWindow(window);
    }
    void CloseWindow(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(Window.GetWindow((DependencyObject)sender));
}
