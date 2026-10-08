using Microsoft.Extensions.DependencyInjection;

namespace SchoolMessenger.Mobile;

public partial class App : Application
{
	public App()
	{
#if IOS
        PrepareIosStorage();
#endif
		InitializeComponent();
	}

#if IOS
    internal static void PrepareIosStorage()
    {
        if (SecureStorage.Default is SecureStorageImplementation storage)
            storage.DefaultAccessible = Security.SecAccessible.WhenUnlockedThisDeviceOnly;
        // iOS Keychain survives uninstall. A fresh installation must not inherit a prior login.
        if (!Preferences.Default.ContainsKey("ios-install-initialized"))
        {
            SecureStorage.Default.RemoveAll();
            Preferences.Default.Set("ios-install-initialized", true);
        }
    }
#endif

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new NavigationPage(new MessengerPage()) { BarBackgroundColor = Color.FromArgb("#1c334f"), BarTextColor = Colors.White });
		window.Stopped += (_, _) => MessengerPage.Active?.Cover();
		window.Resumed += async (_, _) => { if (MessengerPage.Active is { } page) await page.Resume(); };
		return window;
	}
}
