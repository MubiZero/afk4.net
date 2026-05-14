namespace AFK4.Platform.Api.Data;

public sealed class DeviceUpdateStatusEntity
{
    public Guid DeviceUpdateStatusId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public Guid UpdateRolloutId { get; set; }

    public Guid UpdatePackageId { get; set; }

    public string Component { get; set; } = string.Empty;

    public string InstalledVersion { get; set; } = string.Empty;

    public string TargetVersion { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset FirstReportedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
