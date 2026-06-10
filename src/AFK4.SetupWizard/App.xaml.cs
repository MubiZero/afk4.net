using System.Net.Http;
using System.Windows;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Web;

namespace AFK4.SetupWizard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
                Preview.PreviewSetupWizard.CreateShellProvisioner());
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
        var bridge = new SetupWizardWebHostBridge(
            new SetupWizardApiClient(httpClient),
            new FileDeviceKeyStore(),
            new CompositeBootstrapWriter(
                new FileBootstrapWriter(machineInfo.MachineName),
                new EnvironmentBootstrapWriter(machineInfo.MachineName)),
            machineInfo,
            new AgentServiceCompletionAction(),
            new MsiexecPlayerShellProvisioner(
                new SetupWizardPayloadResolver(AppContext.BaseDirectory),
                new SystemProcessRunner()));

        LaunchWebShell(bridge, machineInfo, SetupWizardDefaults.PlatformBaseUrl, isPreview: false);
        base.OnStartup(e);
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
