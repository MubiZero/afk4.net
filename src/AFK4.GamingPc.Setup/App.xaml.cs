using System.Net.Http;
using System.Windows;
using AFK4.GamingPc.Setup.Core;

namespace AFK4.GamingPc.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!ElevationGuard.EnsureElevated())
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    public static SetupShellViewModel CreateViewModel()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = StagingSetupDefaults.PlatformBaseUrl
        };

        var apiClient = new SetupApiClient(httpClient);
        var resources = new SetupEmbeddedResources(typeof(App).Assembly);
        var orchestrator = new GamingPcSetupOrchestrator(
            apiClient,
            new GamingPcMsiInstaller(resources),
            new AgentMachineConfigurationWriter(),
            new WindowsServiceController(),
            TimeProvider.System);

        return new SetupShellViewModel(orchestrator, resources);
    }
}
