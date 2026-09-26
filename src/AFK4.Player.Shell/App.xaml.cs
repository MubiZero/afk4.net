using System.Windows;
using System.Windows.Threading;
using AFK4.Localization;

namespace AFK4.Player.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Одна оболочка на сессию. В киоске её запускает Windows как оболочку учётки игрока, а
    /// супервизор агента поднимает её же, если не видит, — на входе они могут успеть оба. Вторая
    /// копия дралась бы с первой за канал агента (он пускает один экземпляр) и за экран.
    /// </summary>
    private const string SingleInstanceName = @"Local\AFK4.PlayerShell.Host";

    private Mutex? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var first);
        if (!first)
        {
            PlayerShellStartupLog.Write("Another Player Shell already runs in this session; this copy exits.");
            Shutdown();
            return;
        }

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

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PlayerShellStartupLog.Write("Unhandled dispatcher exception in Player Shell.", e.Exception);
        e.Handled = true;
    }
}
