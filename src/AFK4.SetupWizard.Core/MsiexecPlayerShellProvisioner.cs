namespace AFK4.SetupWizard.Core;

public sealed class MsiexecPlayerShellProvisioner(
    SetupWizardPayloadResolver payloadResolver,
    IProcessRunner processRunner) : ISetupWizardShellProvisioner
{
    public ShellProvisionResult Provision() =>
        MsiexecProvisioning.Install(
            payloadResolver.ResolvePlayerShellMsiPath(),
            processRunner,
            "Bundled Player Shell MSI was not found next to the wizard.");
}
