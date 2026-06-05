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
            LaunchWebShell(
                new SetupWizardWebHostBridge(Preview.PreviewSetupWizard.CreateApiClient()),
                new SetupWizardMachineInfo("PREVIEW-PC"),
                SetupWizardDefaults.PlatformBaseUrl,
                isPreview: true);
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
        var apiClient = new SetupWizardApiClient(httpClient);

        LaunchWebShell(
            new SetupWizardWebHostBridge(apiClient),
            machineInfo,
            SetupWizardDefaults.PlatformBaseUrl,
            isPreview: false);
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
