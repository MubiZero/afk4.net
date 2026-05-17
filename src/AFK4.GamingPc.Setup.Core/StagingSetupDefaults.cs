namespace AFK4.GamingPc.Setup.Core;

public static class StagingSetupDefaults
{
    public static readonly Uri PlatformBaseUrl = new("https://afk4.staging.mubi.dev");

    public static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");

    public static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    public const string EnvironmentName = "AFK4 Staging";

    public const string AgentServiceName = "AFK4.Agent.Service";

    public const string AgentVersion = "0.1.0";

    public const string ShellVersion = "0.1.0";

    public const string UpdateChannel = "internal";

    public const string PlayerShellExecutablePath = @"C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe";

    public const string UpdateInstallerExecutablePath = "powershell.exe";

    public const string UpdateInstallerArgumentsTemplate =
        "-NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\AFK4\\Update Helpers\\install-afk4-update-msi.ps1\" -PackagePath \"{PackagePath}\" -Component \"{Component}\" -Version \"{Version}\"";

    public const string UpdateRollbackExecutablePath = "powershell.exe";

    public const string UpdateRollbackArgumentsTemplate =
        "-NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\AFK4\\Update Helpers\\rollback-afk4-update-msi.ps1\" -PackagePath \"{PackagePath}\" -Component \"{Component}\" -Version \"{Version}\"";

    public const string UpdateRestartExecutablePath = "powershell.exe";

    public const string UpdateRestartArgumentsTemplate =
        "-NoProfile -ExecutionPolicy Bypass -File \"C:\\Program Files\\AFK4\\Update Helpers\\restart-afk4-agent-service.ps1\"";
}
