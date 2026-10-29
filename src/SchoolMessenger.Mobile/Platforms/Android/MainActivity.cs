using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SchoolMessenger.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetFlags(Android.Views.WindowManagerFlags.Secure,Android.Views.WindowManagerFlags.Secure);
        OnBackPressedDispatcher.AddCallback(this,new NativeBackNavigation(this));
    }
    sealed class NativeBackNavigation(MainActivity activity):AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            var navigation=Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page as NavigationPage;
            if(navigation?.Navigation.NavigationStack.Count>1)
                MainThread.BeginInvokeOnMainThread(async()=>await navigation.PopAsync());
            else activity.Finish();
        }
    }
}
