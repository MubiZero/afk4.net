using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Web;

namespace AFK4.SetupWizard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Backstop for anything that escapes the per-message guards (e.g. an unexpected failure
        // during enrollment). Log it and show a message instead of a raw .NET crash dialog.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

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

        var machineInfo = new SetupWizardMachineInfo(Environment.MachineName);
        var httpClient = new HttpClient
        {
            BaseAddress = SetupWizardDefaults.PlatformBaseUrl
        };
        var payloadResolver = new SetupWizardPayloadResolver(AppContext.BaseDirectory);
        var processRunner = new SystemProcessRunner();
        var bridge = new SetupWizardWebHostBridge(
            new SetupWizardApiClient(httpClient),
            new FileDeviceKeyStore(),
            new CompositeBootstrapWriter(
                new FileBootstrapWriter(machineInfo.MachineName),
                new EnvironmentBootstrapWriter(machineInfo.MachineName)),
            machineInfo,
            new AgentServiceCompletionAction(),
            new MsiexecPlayerShellProvisioner(payloadResolver, processRunner),
            new MsiexecOrganizationAdminProvisioner(payloadResolver, processRunner),
            new ExplorerOrganizationAdminLauncher());

        LaunchWebShell(bridge, machineInfo, SetupWizardDefaults.PlatformBaseUrl, isPreview: false);
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SetupWizardStartupLog.Write("Unhandled dispatcher exception in the setup wizard.", e.Exception);

        // Mark handled so WPF doesn't tear the process down with a raw crash dialog. Best-effort
        // message to the user; the device may be partially enrolled — the log holds the detail.
        MessageBox.Show(
            $"Произошла непредвиденная ошибка мастера настройки:\n\n{e.Exception.Message}",
            "AFK4.NET — Мастер настройки",
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
