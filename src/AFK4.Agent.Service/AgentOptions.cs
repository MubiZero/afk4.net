namespace AFK4.Agent.Service;

public sealed class AgentOptions
{
    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5000");

    public Guid OrganizationId { get; init; }

    public Guid BranchId { get; init; }

    public Guid DeviceId { get; init; }

    public string MachineName { get; init; } = Environment.MachineName;

    public string AgentVersion { get; init; } = "0.1.0";

    public string ShellVersion { get; init; } = "0.1.0";

    public string DeviceCredentialSecret { get; init; } = string.Empty;

    public string LeaseSigningPublicKeyPem { get; init; } = string.Empty;

    public string StateDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent");

    public string PlayerShellExecutablePath { get; init; } = string.Empty;

    public string PlayerShellStartArguments { get; init; } = string.Empty;

    public string PlayerShellPipeName { get; init; } = "afk4-player-shell";

    public string PlayerShellCommandPipeName { get; init; } = "afk4-player-shell-commands";

    public int PlayerShellPipeConnectionTimeoutMilliseconds { get; init; } = 250;

    public List<AgentLauncherAppOptions> LauncherApps { get; init; } = [];

    public List<string> DeniedProcessNames { get; init; } = [];

    public string UpdateChannel { get; init; } = "stable";
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
