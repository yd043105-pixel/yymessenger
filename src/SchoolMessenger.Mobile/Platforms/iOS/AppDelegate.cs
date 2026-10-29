using Foundation;

using UIKit;

namespace SchoolMessenger.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    UIView? privacyCover;

    public override void OnResignActivation(UIApplication application)
    {
        // Cover the native window, including titles and dialogs, before the iOS background snapshot.
        var window = application.ConnectedScenes.OfType<UIWindowScene>().SelectMany(scene => scene.Windows).FirstOrDefault(w => w.IsKeyWindow) ?? Window;
        if (window is not null && privacyCover is null)
        {
            privacyCover = new UIView(window.Bounds) { BackgroundColor = UIColor.FromRGB(28, 51, 79), AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight };
            window.AddSubview(privacyCover);
        }
        base.OnResignActivation(application);
    }

    public override void OnActivated(UIApplication application)
    {
        privacyCover?.RemoveFromSuperview();
        privacyCover?.Dispose();
        privacyCover = null;
        base.OnActivated(application);
    }

#if DEBUG && IOS_VERIFY
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        var launched = base.FinishedLaunching(application, launchOptions);
        if (Environment.GetEnvironmentVariable("YY_IOS_VERIFY") is { Length: > 0 } phase)
            MainThread.BeginInvokeOnMainThread(async () => await IosRuntimeChecks.Run(phase));
        return launched;
    }
#endif
}
