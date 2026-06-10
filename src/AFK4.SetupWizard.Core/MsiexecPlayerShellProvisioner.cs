namespace AFK4.SetupWizard.Core;

public sealed class MsiexecPlayerShellProvisioner(
    SetupWizardPayloadResolver payloadResolver,
    IProcessRunner processRunner) : ISetupWizardShellProvisioner
{
    private const int Success = 0;
    private const int SuccessRebootRequired = 3010;
    private const int ProductAlreadyInstalled = 1638;

    public ShellProvisionResult Provision()
    {
        var msiPath = payloadResolver.ResolvePlayerShellMsiPath();
        if (msiPath is null)
        {
            return ShellProvisionResult.Failed(null, "Bundled Player Shell MSI was not found next to the wizard.");
        }

        var result = processRunner.Run("msiexec.exe", ["/i", msiPath, "/qn"]);
        return result.ExitCode switch
        {
            Success or SuccessRebootRequired => ShellProvisionResult.Installed(result.ExitCode),
            ProductAlreadyInstalled => ShellProvisionResult.AlreadyPresent(result.ExitCode),
            _ => ShellProvisionResult.Failed(result.ExitCode, result.Output)
        };
    }
}
