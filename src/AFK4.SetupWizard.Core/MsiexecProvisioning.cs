namespace AFK4.SetupWizard.Core;

// Shared msiexec install for the bundled payload MSIs (Player Shell, Organization Admin): run a silent
// per-machine install and map the exit code to a provisioning result. Kept in one place so both
// role provisioners agree on the exit-code vocabulary.
internal static class MsiexecProvisioning
{
    private const int Success = 0;
    private const int SuccessRebootRequired = 3010;
    private const int ProductAlreadyInstalled = 1638;

    /// <summary>ERROR_INSTALL_ALREADY_RUNNING: Windows Installer занят другой установкой.</summary>
    private const int AnotherInstallInProgress = 1618;

    /// <summary>
    /// Сколько ждать занятый Windows Installer: две минуты по пять секунд. При тихой установке
    /// мастер запускается, пока установщик агента ещё закрывает свою сессию, — первая попытка
    /// почти всегда натыкается на него. Да и в окне мастера рядом бывает Windows Update.
    /// </summary>
    internal const int BusyAttempts = 24;

    internal static readonly TimeSpan BusyRetryDelay = TimeSpan.FromSeconds(5);

    public static ShellProvisionResult Install(
        string? msiPath,
        IProcessRunner processRunner,
        string missingMessage,
        Action<TimeSpan>? wait = null)
    {
        if (msiPath is null)
        {
            return ShellProvisionResult.Failed(null, missingMessage);
        }

        var result = processRunner.Run("msiexec.exe", ["/i", msiPath, "/qn"]);
        for (var attempt = 1; result.ExitCode == AnotherInstallInProgress && attempt < BusyAttempts; attempt++)
        {
            (wait ?? Thread.Sleep)(BusyRetryDelay);
            result = processRunner.Run("msiexec.exe", ["/i", msiPath, "/qn"]);
        }

        return result.ExitCode switch
        {
            Success or SuccessRebootRequired => ShellProvisionResult.Installed(result.ExitCode),
            ProductAlreadyInstalled => ShellProvisionResult.AlreadyPresent(result.ExitCode),
            _ => ShellProvisionResult.Failed(result.ExitCode, result.Output)
        };
    }
}
