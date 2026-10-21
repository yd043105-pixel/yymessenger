using System.Windows;

namespace SchoolMessenger.Desktop;

public partial class MainWindow
{
    void OpenFeedback(object sender,RoutedEventArgs e){if(Api is not null)new FeedbackWindow(this).Show();}
}
