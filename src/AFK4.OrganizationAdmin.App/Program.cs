using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AFK4.OrganizationAdmin.App;
using AFK4.OrganizationAdmin.App.Configuration;
using AFK4.OrganizationAdmin.App.Connection;
using AFK4.OrganizationAdmin.App.Offline;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.OrganizationAdmin.Web;
using AFK4.WebHost;

return OrganizationAdminProgram.Run();

internal static partial class OrganizationAdminProgram
{
    /// <summary>
    /// Одна Панель на сеанс. Она стартует сама при входе в Windows, и кассир, не заметив её за другими окнами, жмёт ярлык:
    /// вторая копия открыла бы второе подключение к залу и дралась бы с первой за канал обновлений. Повторный запуск выводит
    /// уже открытую Панель вперёд и выходит.
    /// </summary>
    private const string SingleInstanceName = @"Local\AFK4.OrganizationAdmin.Host";

    private const string Caption = "Панель AFK4.NET";

    [STAThread]
    public static int Run()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var first);
        if (!first)
        {
            BringRunningCopyForward();
            return 0;
        }

        // Последняя страховка: сбой, который не поймал ни один обработчик, иначе роняет Панель без следа.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            OrganizationAdminStartupLog.Write("Unhandled exception in Organization Admin host.", args.ExceptionObject as Exception);

        NamedPipeUpdateCoordinationServer? updateCoordinationServer = null;
        try
        {
            var appOptions = OrganizationAdminOptions.LoadFromEnvironment();
            var shellOptions = OrganizationAdminWebShellOptions.LoadFromEnvironment();
            var updateActivityState = new OrganizationAdminActivityState();
            var acknowledgementStore = new FileOrganizationAdminShutdownAcknowledgementStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AFK4",
                "OrganizationAdmin",
                "update-shutdown-acknowledgement.json"));

            WebHostWindow.UiError += exception => OrganizationAdminStartupLog.Write("Organization Admin UI work failed.", exception);
            WebHostWindow.Initialize();

            OrganizationAdminWindow? window = null;
            Action requestShutdown = () => WebHostWindow.RunOnUiThread(() => window?.Close());
            var bridge = CreateHostBridge(updateActivityState, acknowledgementStore, requestShutdown);
            window = new OrganizationAdminWindow(appOptions, shellOptions, new OrganizationAdminWebAssetResolver(AppContext.BaseDirectory), bridge);

            var secret = Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET");
            if (!string.IsNullOrWhiteSpace(secret))
            {
                var pipeName = Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_PIPE_NAME")
                    ?? "afk4-organization-admin-updates";
                updateCoordinationServer = new NamedPipeUpdateCoordinationServer(
                    pipeName,
                    secret,
                    updateActivityState,
                    acknowledgementStore,
                    requestShutdown);
                updateCoordinationServer.Start();
            }

            window.Show();
            var exitCode = WebHostWindow.RunMessageLoop();
            OrganizationAdminStartupLog.Write($"Organization Admin window closed, exit code {exitCode}.");
            return exitCode;
        }
        catch (Exception exception)
        {
            OrganizationAdminStartupLog.Write("Organization Admin failed to start.", exception);
            MessageBox(IntPtr.Zero, $"Не удалось запустить Панель AFK4.NET:\n\n{exception.Message}", Caption, 0x10);
            return 1;
        }
        finally
        {
            updateCoordinationServer?.Dispose();
        }
    }

    private static OrganizationAdminWebHostBridge CreateHostBridge(
        OrganizationAdminActivityState activityState,
        IOrganizationAdminShutdownAcknowledgementStore acknowledgementStore,
        Action requestShutdown)
    {
        // Auth no longer runs through this bridge — the web UI signs itself in over plain HTTP (see
        // OrganizationAdminWebHostBridge). Only device-identity (machine/seat pinning) stays native-side.
        // Ключ офлайн-аренд создаётся при первом обращении, а не на старте: Панели без связи он нужен не каждый день, и окно
        // не должно ждать DPAPI. ПК в зале отвечает по сети клуба за миллисекунды — пяти секунд хватает.
        var offlineKey = new Lazy<OfflineDeskKey>(() => OfflineDeskKey.LoadOrCreate(OfflineDeskKey.DefaultPath()));
        var offlineDesk = new OfflineDesk(
            () => offlineKey.Value,
            new HttpClient { Timeout = TimeSpan.FromSeconds(5) });
        return new OrganizationAdminWebHostBridge(
            new ProtectedDataOrganizationAdminConnectionStore(),
            activityState,
            acknowledgementStore,
            requestShutdown,
            offlineDesk,
            new OfflineJournal(OfflineJournal.DefaultPath()));
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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr window, string text, string caption, uint type);
}
