using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Core.Kiosk;
using AFK4.SetupWizard.Core.SilentInstall;
using AFK4.SetupWizard.Web;

namespace AFK4.SetupWizard;

public partial class App : Application
{
    // Тихая установка: окон нет, и сообщение об ошибке некому закрыть — скрипт развёртывания
    // висел бы на нём до таймаута.
    private bool silent;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Backstop for anything that escapes the per-message guards (e.g. an unexpected failure
        // during enrollment). Log it and show a message instead of a raw .NET crash dialog.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var silentInstall = SilentInstallOptions.Parse(e.Args);
        if (silentInstall.Requested)
        {
            silent = true;
            Shutdown(RunSilentInstall(silentInstall));
            return;
        }

#if DEBUG
        if (e.Args.Contains("--preview"))
        {
            var previewMachine = new SetupWizardMachineInfo("PREVIEW-PC");
            var previewBridge = new SetupWizardWebHostBridge(
                Preview.PreviewSetupWizard.CreateApiClient(),
                Preview.PreviewSetupWizard.CreateDeviceKeyStore(),
                Preview.PreviewSetupWizard.CreateBootstrapWriter(),
                previewMachine,
                Preview.PreviewSetupWizard.CreateCompletionAction(),
                Preview.PreviewSetupWizard.CreateShellProvisioner(),
                Preview.PreviewSetupWizard.CreateShellProvisioner(),
                Preview.PreviewSetupWizard.CreateOperatorLauncher());
            LaunchWebShell(previewBridge, previewMachine, SetupWizardDefaults.PlatformBaseUrl, isPreview: true);
            base.OnStartup(e);
            return;
        }
#endif

        if (!ElevationGuard.EnsureElevated())
        {
            Shutdown();
            return;
        }

        var machine = new WizardMachine();
        var bridge = new SetupWizardWebHostBridge(
            machine.ApiClient,
            machine.KeyStore,
            machine.BootstrapWriter,
            machine.Info,
            machine.CompletionAction,
            machine.ShellProvisioner,
            machine.OperatorProvisioner,
            machine.OperatorLauncher,
            new OpenFileDialogLogoPicker(),
            machine.Kiosk,
            new ShutdownRebootAction(machine.ProcessRunner));

        LaunchWebShell(bridge, machine.Info, SetupWizardDefaults.PlatformBaseUrl, isPreview: false);
        base.OnStartup(e);
    }

    /// <summary>
    /// <c>--install-code</c>: установка без окна (план P5f-2). Прав администратора не просим —
    /// окна UAC на ПК, который ставят скриптом, никто не увидит; без них выходим с кодом.
    /// </summary>
    private static int RunSilentInstall(SilentInstallParse silentInstall)
    {
        if (silentInstall.Options is null)
        {
            SetupWizardStartupLog.Write($"Silent install could not start: {silentInstall.Error}");
            return SilentInstallExitCodes.BadArguments;
        }

        if (!ElevationGuard.IsElevated())
        {
            SetupWizardStartupLog.Write("Silent install needs administrator rights: run the installer from an elevated deployment tool.");
            return SilentInstallExitCodes.NotElevated;
        }

        var machine = new WizardMachine();
        var installer = new SilentInstaller(
            machine.ApiClient,
            machine.KeyStore,
            machine.Info,
            new SetupWizardDeviceSetup(
                machine.BootstrapWriter,
                machine.CompletionAction,
                machine.ShellProvisioner,
                machine.OperatorProvisioner,
                machine.OperatorLauncher,
                machine.Kiosk));

        try
        {
            // Окна нет, и ждать в потоке запуска можно: ни одно сообщение ему не придёт.
            return Task.Run(() => installer.RunAsync(silentInstall.Options, CancellationToken.None)).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            SetupWizardStartupLog.Write("Silent install failed unexpectedly.", exception);
            return SilentInstallExitCodes.SetupFailed;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SetupWizardStartupLog.Write("Unhandled dispatcher exception in the setup wizard.", e.Exception);
        if (silent)
        {
            e.Handled = true;
            Shutdown(SilentInstallExitCodes.SetupFailed);
            return;
        }

        // Mark handled so WPF doesn't tear the process down with a raw crash dialog. Best-effort
        // message to the user; the device may be partially enrolled — the log holds the detail.
        MessageBox.Show(
            $"Произошла непредвиденная ошибка мастера установки:\n\n{e.Exception.Message}",
            "Мастер установки AFK4.NET",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LaunchWebShell(
        SetupWizardWebHostBridge hostBridge,
        SetupWizardMachineInfo machineInfo,
        Uri platformBaseUrl,
        bool isPreview)
    {
        var window = new WebViewSetupWindow(
            SetupWizardWebShellOptions.LoadFromEnvironment(),
            new SetupWizardWebAssetResolver(AppContext.BaseDirectory),
            hostBridge,
            machineInfo,
            platformBaseUrl,
            isPreview);
        window.Show();
    }
}
