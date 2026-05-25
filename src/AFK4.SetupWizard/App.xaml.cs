using System.Net.Http;
using System.Windows;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
        var viewModel = new SetupWizardViewModel(
            new SetupWizardApiClient(httpClient),
            new FileDeviceKeyStore(),
            new EnvironmentBootstrapWriter(machineInfo.MachineName),
            machineInfo,
            new AgentServiceCompletionAction());

        var window = new MainWindow(viewModel);
        window.Show();
        base.OnStartup(e);
    }
}
