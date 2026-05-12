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
}
