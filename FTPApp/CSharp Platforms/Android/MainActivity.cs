#if ANDROID
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Make activity fullscreen and enable immersive mode
        Window.AddFlags(WindowManagerFlags.Fullscreen);
        Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
            SystemUiFlags.LayoutStable |
            SystemUiFlags.LayoutFullscreen |
            SystemUiFlags.ImmersiveSticky);
    }
}
#endif