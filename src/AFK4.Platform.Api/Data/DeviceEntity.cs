namespace AFK4.Platform.Api.Data;

public sealed class DeviceEntity
{
    public Guid DeviceId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string AgentVersion { get; set; } = string.Empty;

    public string ShellVersion { get; set; } = string.Empty;

    public DateTimeOffset EnrolledAtUtc { get; set; }

    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }

    public bool IsOnline { get; set; }

    public bool IsLocked { get; set; }
}
