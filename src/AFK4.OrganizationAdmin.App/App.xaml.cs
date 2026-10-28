using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using AFK4.OrganizationAdmin.App.Updates;

namespace AFK4.OrganizationAdmin.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Одна Панель на сеанс. Она стартует сама при входе в Windows, и кассир, не заметив её за
    /// другими окнами, жмёт ярлык: вторая копия открыла бы второе подключение к залу и дралась бы
    /// с первой за канал обновлений. Повторный запуск выводит уже открытую Панель вперёд и выходит.
    /// </summary>
    private const string SingleInstanceName = @"Local\AFK4.OrganizationAdmin.Host";

    private Mutex? singleInstance;
    private NamedPipeUpdateCoordinationServer? updateCoordinationServer;
    internal OrganizationAdminActivityState UpdateActivityState { get; } = new();
    internal IOrganizationAdminShutdownAcknowledgementStore UpdateAcknowledgementStore { get; } =
        new FileOrganizationAdminShutdownAcknowledgementStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AFK4",
            "OrganizationAdmin",
            "update-shutdown-acknowledgement.json"));

    protected override void OnStartup(StartupEventArgs e)
    {
#if NETFRAMEWORK
        NetFrameworkCompat.UseTls12OnWindows7();
#endif
        singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var first);
        if (!first)
        {
            BringRunningCopyForward();
            StartupUri = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Backstop: an exception escaping a UI callback (e.g. the async-void bridge handler)
        // would otherwise tear down the WebView host with no record of why. Log it and keep the
        // dispatcher alive so a single bad message can't crash the app on every launch.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var secret = Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET");
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var pipeName = Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_PIPE_NAME")
                ?? "afk4-organization-admin-updates";
            updateCoordinationServer = new NamedPipeUpdateCoordinationServer(
                pipeName,
                secret,
                UpdateActivityState,
                UpdateAcknowledgementStore,
                () => Dispatcher.BeginInvoke(new Action(Shutdown)));
            updateCoordinationServer.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        updateCoordinationServer?.Dispose();
        singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void BringRunningCopyForward()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            using (other)
            {
                if (other.Id == current.Id || other.SessionId != current.SessionId || other.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                // Из свёрнутого — вернуть; развёрнутую на весь экран не трогать, только вывести вперёд.
                if (IsIconic(other.MainWindowHandle))
                {
                    ShowWindow(other.MainWindowHandle, SwRestore);
                }

                SetForegroundWindow(other.MainWindowHandle);
                return;
            }
        }
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        OrganizationAdminStartupLog.Write("Unhandled dispatcher exception in Organization Admin host.", e.Exception);
        e.Handled = true;
    }
}
