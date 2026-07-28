namespace AFK4.SetupWizard.Core;

// Shared msiexec install for the bundled payload MSIs (Player Shell, Organization Admin): run a silent
// per-machine install and map the exit code to a provisioning result. Kept in one place so both
// role provisioners agree on the exit-code vocabulary.
internal static class MsiexecProvisioning
{
    private const int Success = 0;
    private const int SuccessRebootRequired = 3010;
    private const int ProductAlreadyInstalled = 1638;

    public static ShellProvisionResult Install(string? msiPath, IProcessRunner processRunner, string missingMessage)
    {
        if (msiPath is null)
        {
            return ShellProvisionResult.Failed(null, missingMessage);
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
