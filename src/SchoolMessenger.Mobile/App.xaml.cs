using Microsoft.Extensions.DependencyInjection;

namespace SchoolMessenger.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new NavigationPage(new MessengerPage()) { BarBackgroundColor = Color.FromArgb("#1c334f"), BarTextColor = Colors.White });
		window.Stopped += (_, _) => MessengerPage.Active?.Cover();
		window.Resumed += async (_, _) => { if (MessengerPage.Active is { } page) await page.Resume(); };
		return window;
	}
}
