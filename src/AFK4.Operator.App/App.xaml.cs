using System.Windows;
using System.Windows.Threading;

namespace AFK4.Operator.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Backstop: an exception escaping a UI callback (e.g. the async-void bridge handler)
        // would otherwise tear down the WebView host with no record of why. Log it and keep the
        // dispatcher alive so a single bad message can't crash the app on every launch.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        OperatorStartupLog.Write("Unhandled dispatcher exception in operator host.", e.Exception);
        e.Handled = true;
    }
}

