namespace AFK4.SetupWizard.Core;

public enum ShellProvisionStatus
{
    Installed,
    AlreadyPresent,
    Failed
}

public sealed record ShellProvisionResult(ShellProvisionStatus Status, int? ExitCode, string? Message)
{
    public static ShellProvisionResult Installed(int exitCode) => new(ShellProvisionStatus.Installed, exitCode, null);

    public static ShellProvisionResult AlreadyPresent(int exitCode) => new(ShellProvisionStatus.AlreadyPresent, exitCode, null);

    public static ShellProvisionResult Failed(int? exitCode, string? message) => new(ShellProvisionStatus.Failed, exitCode, message);
}

public interface ISetupWizardShellProvisioner
{
    ShellProvisionResult Provision();
}

public sealed record ProcessRunResult(int ExitCode, string Output);

public interface IProcessRunner
{
    ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments);
}
