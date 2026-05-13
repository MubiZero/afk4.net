namespace AFK4.Platform.Api.Data;

public sealed class DeviceInstalledAppEntity
{
    public Guid DeviceInstalledAppId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public string? InstallLocation { get; set; }

    public DateTimeOffset? InstalledAtUtc { get; set; }

    public DateTimeOffset ReportedAtUtc { get; set; }
}
