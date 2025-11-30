using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui;

namespace FTPApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var window = Window;
            if (window == null)
                return;

            // Do NOT set Fullscreen or ImmersiveSticky if you want system UI (status/navigation)
            window.ClearFlags(WindowManagerFlags.Fullscreen);

            // Allow content to layout behind the status bar but keep the bars visible
            var decor = window.DecorView;
            if (decor != null)
            {
                decor.SystemUiFlags = SystemUiFlags.LayoutStable | SystemUiFlags.LayoutFullscreen;
            }
        }
    }
}
