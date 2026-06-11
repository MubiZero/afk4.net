using System.Windows;
using System.Windows.Threading;
using AFK4.Localization;

namespace AFK4.Player.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Last-resort backstop: a dispatcher exception that escapes the guarded WebView handlers
        // would otherwise crash the kiosk and trigger an agent-supervised relaunch loop. Log it and
        // keep the window up; the WebView watchdog / agent still recover genuinely broken states.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            var localization = LocalizationService.LoadEmbedded(Locales.Default);
            LocalizationScope.Current = localization;

            new AFK4.Player.Shell.Web.WebViewPlayerWindow().Show();
            base.OnStartup(e);
        }
        catch (Exception exception)
        {
            PlayerShellStartupLog.Write("Player Shell failed to start.", exception);
            throw;
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PlayerShellStartupLog.Write("Unhandled dispatcher exception in Player Shell.", e.Exception);
        e.Handled = true;
    }
}
