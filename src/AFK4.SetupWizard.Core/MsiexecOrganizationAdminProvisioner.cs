namespace AFK4.SetupWizard.Core;

// Installs the bundled Organization Admin MSI on a manager/cashier workstation enrollment, mirroring
// MsiexecPlayerShellProvisioner for gaming PCs. The operator app reads its organization/branch/platform
// from machine env vars (written by EnvironmentBootstrapWriter during enroll), so a silent install
// of the MSI is all that's left to make the cashier UI runnable.
public sealed class MsiexecOrganizationAdminProvisioner(
    SetupWizardPayloadResolver payloadResolver,
    IProcessRunner processRunner) : ISetupWizardShellProvisioner
{
    public ShellProvisionResult Provision() =>
        MsiexecProvisioning.Install(
            payloadResolver.ResolveOrganizationAdminMsiPath(),
            processRunner,
            "Bundled Organization Admin MSI was not found next to the wizard.");
}
