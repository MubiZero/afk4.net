using System.Net.Http;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Core.Kiosk;

namespace AFK4.SetupWizard;

/// <summary>
/// Настоящие зависимости мастера на этом ПК — одни и те же для окна и для тихой установки: разные
/// наборы однажды разошлись бы, и один из путей писал бы настройку не туда.
/// </summary>
internal sealed class WizardMachine
{
    public WizardMachine()
    {
        Info = new SetupWizardMachineInfo(Environment.MachineName);
        ApiClient = new SetupWizardApiClient(new HttpClient { BaseAddress = SetupWizardDefaults.PlatformBaseUrl });
        BootstrapWriter = new CompositeBootstrapWriter(
            new FileBootstrapWriter(Info.MachineName),
            new EnvironmentBootstrapWriter(Info.MachineName));
        var payloadResolver = new SetupWizardPayloadResolver(AppContext.BaseDirectory);
        ShellProvisioner = new MsiexecPlayerShellProvisioner(payloadResolver, ProcessRunner);
        OperatorProvisioner = new MsiexecOrganizationAdminProvisioner(payloadResolver, ProcessRunner);
    }

    public SetupWizardMachineInfo Info { get; }

    public SetupWizardApiClient ApiClient { get; }

    public FileDeviceKeyStore KeyStore { get; } = new();

    public ISetupWizardBootstrapWriter BootstrapWriter { get; }

    public SystemProcessRunner ProcessRunner { get; } = new();

    public AgentServiceCompletionAction CompletionAction { get; } = new();

    public MsiexecPlayerShellProvisioner ShellProvisioner { get; }

    public MsiexecOrganizationAdminProvisioner OperatorProvisioner { get; }

    public ExplorerOrganizationAdminLauncher OperatorLauncher { get; } = new();

    public KioskProvisioner Kiosk { get; } = new(new WindowsKioskMachine(), new FileKioskStateStore(), new FileKioskAgentConfig());
}
