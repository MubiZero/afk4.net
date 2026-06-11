using AFK4.Shared.Contracts.Install;

namespace AFK4.Agent.Service;

public sealed class AgentOptions
{
    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5000");

    public Guid OrganizationId { get; init; }

    public Guid BranchId { get; init; }

    public Guid DeviceId { get; init; }

    public string MachineName { get; init; } = Environment.MachineName;

    public string AgentVersion { get; init; } = "0.1.0";

    public string ShellVersion { get; init; } = string.Empty;

    public string OperatorAppVersion { get; init; } = string.Empty;

    public string DeviceRole { get; init; } = DeviceRoleNames.GamingPc;

    /// <summary>
    /// The branch's preferred customer-shell locale (mirrors <c>BranchEntity.PreferredLocale</c>),
    /// provisioned into the agent config and stamped onto the player-shell state so the kiosk
    /// renders in the branch language. Defaults to <c>ru</c>.
    /// </summary>
    public string PreferredLocale { get; init; } = "ru";

    /// <summary>
    /// How many seconds of remaining session time trigger a low-time warning in the player shell.
    /// Defaults to 300 (5 minutes).
    /// </summary>
    public int ShellWarningThresholdSeconds { get; init; } = 300;

    public string? ClubName { get; init; }

    public string? LogoUrl { get; init; }

    public string? AccentColor { get; init; }

    public string DeviceCredentialSecret { get; init; } = string.Empty;

    public string LeaseSigningPublicKeyPem { get; init; } = string.Empty;

    public string StateDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent");

    public string PlayerShellExecutablePath { get; init; } = string.Empty;

    public string PlayerShellStartArguments { get; init; } = string.Empty;

    public bool PlayerShellAutoStartEnabled { get; init; } = true;

    public string PlayerShellPipeName { get; init; } = "afk4-player-shell";

    public string PlayerShellCommandPipeName { get; init; } = "afk4-player-shell-commands";

    public int PlayerShellPipeConnectionTimeoutMilliseconds { get; init; } = 250;

    public List<AgentLauncherAppOptions> LauncherApps { get; init; } = [];

    public List<string> DeniedProcessNames { get; init; } = [];

    public string UpdateChannel { get; init; } = "stable";

    public string UpdateStagingDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent",
        "Updates");

    public string UpdateInstallerExecutablePath { get; init; } = string.Empty;

    public string UpdateInstallerArgumentsTemplate { get; init; } = "\"{PackagePath}\" --component \"{Component}\" --version \"{Version}\"";

    public int UpdateInstallerTimeoutSeconds { get; init; } = 1800;

    public int UpdateCheckIntervalSeconds { get; init; } = 300;

    public string UpdateStateDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent",
        "UpdateState");

    public string UpdateRollbackExecutablePath { get; init; } = string.Empty;

    public string UpdateRollbackArgumentsTemplate { get; init; } = "--component \"{Component}\" --version \"{TargetVersion}\" --artifact \"{ArtifactPath}\"";

    public string UpdateRestartExecutablePath { get; init; } = string.Empty;

    public string UpdateRestartArgumentsTemplate { get; init; } = "--component \"{Component}\" --version \"{TargetVersion}\"";

    public string UpdatePackageSigningPublicKeyPem { get; init; } = string.Empty;

    /// <summary>
    /// True when the agent has a usable enrollment: real org/branch/device identifiers and an
    /// https platform URL that is not the localhost default. When false the workers must stay idle
    /// rather than beacon to localhost — bootstrap.json was missing or unreadable.
    /// </summary>
    public bool IsConfigured =>
        OrganizationId != Guid.Empty
        && BranchId != Guid.Empty
        && DeviceId != Guid.Empty
        && PlatformBaseUrl.Scheme == Uri.UriSchemeHttps
        && !PlatformBaseUrl.IsLoopback;
}

public sealed class AgentLauncherAppOptions
{
    public string AppId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Category { get; init; } = "Games";

    public string ExecutablePath { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;
}
