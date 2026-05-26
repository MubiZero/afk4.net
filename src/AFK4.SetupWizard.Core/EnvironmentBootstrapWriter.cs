using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

public sealed class EnvironmentBootstrapWriter(
    string machineName,
    EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) : ISetupWizardBootstrapWriter
{
    private const string UpdateHelperRoot = @"AFK4\Update Helpers";
    private const string PlayerShellExecutableRelativePath = @"AFK4\Player Shell\AFK4.Player.Shell.exe";
    private const string OperatorPlatformBaseUrlEnvironmentVariable = "AFK4_OPERATOR_PLATFORM_BASE_URL";
    private const string OperatorOrganizationIdEnvironmentVariable = "AFK4_OPERATOR_ORGANIZATION_ID";
    private const string OperatorBranchIdEnvironmentVariable = "AFK4_OPERATOR_BRANCH_ID";

    public void Write(SetupWizardBootstrapConfig config)
    {
        var platformBaseUrl = config.ApiBaseUrl.TrimEnd('/');

        Write("Agent__PlatformBaseUrl", platformBaseUrl);
        Write(OperatorPlatformBaseUrlEnvironmentVariable, platformBaseUrl);
        Write(OperatorOrganizationIdEnvironmentVariable, config.OrganizationId.ToString("D"));
        Write(OperatorBranchIdEnvironmentVariable, config.BranchId.ToString("D"));
        Write("Agent__OrganizationId", config.OrganizationId.ToString("D"));
        Write("Agent__BranchId", config.BranchId.ToString("D"));
        Write("Agent__DeviceId", config.DeviceId.ToString("D"));
        Write("Agent__MachineName", machineName);
        Write("Agent__DeviceRole", string.IsNullOrWhiteSpace(config.Role) ? DeviceRoleNames.GamingPc : config.Role);
        Write("Agent__DeviceCredentialSecret", config.CredentialSecret);
        Write("Agent__LeaseSigningPublicKeyPem", config.LeaseSigningPublicKeyPem);
        Write("Agent__UpdateChannel", config.UpdateChannel);
        Write("Agent__PlayerShellExecutablePath", Path.Combine(ProgramFilesPath(), PlayerShellExecutableRelativePath));
        Write("Agent__PlayerShellAutoStartEnabled", bool.TrueString);
        WriteUpdateHelperConfiguration();
        Write("Agent__UpdatePackageSigningPublicKeyPem", config.UpdatePackageSigningPublicKeyPem);
    }

    private void WriteUpdateHelperConfiguration()
    {
        var powershellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        var helperDirectory = Path.Combine(ProgramFilesPath(), UpdateHelperRoot);

        Write("Agent__UpdateInstallerExecutablePath", powershellPath);
        Write(
            "Agent__UpdateInstallerArgumentsTemplate",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(helperDirectory, "install-afk4-update-msi.ps1")}\" -PackagePath \"{{PackagePath}}\" -Component \"{{Component}}\" -Version \"{{Version}}\"");
        Write("Agent__UpdateRollbackExecutablePath", powershellPath);
        Write(
            "Agent__UpdateRollbackArgumentsTemplate",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(helperDirectory, "rollback-afk4-update-msi.ps1")}\" -PackagePath \"{{PackagePath}}\" -Component \"{{Component}}\" -Version \"{{Version}}\"");
        Write("Agent__UpdateRestartExecutablePath", powershellPath);
        Write(
            "Agent__UpdateRestartArgumentsTemplate",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(helperDirectory, "restart-afk4-agent-service.ps1")}\"");
    }

    private static string ProgramFilesPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return string.IsNullOrWhiteSpace(programFiles)
            ? @"C:\Program Files"
            : programFiles;
    }

    private void Write(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }
}
