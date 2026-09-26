namespace AFK4.Platform.Api.Data;

/// <summary>
/// Железо ПК: последний снимок и принятый. Первый снимок принимается сам — сравнивать не с чем.
/// Снимки лежат JSON: состав меняется вместе с тем, что умеет собирать агент.
/// </summary>
public sealed class DeviceHardwareEntity
{
    public Guid DeviceId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string CurrentJson { get; set; } = string.Empty;

    public string CurrentFingerprint { get; set; } = string.Empty;

    public DateTimeOffset ReportedAtUtc { get; set; }

    public string AcceptedJson { get; set; } = string.Empty;

    public string AcceptedFingerprint { get; set; } = string.Empty;

    public DateTimeOffset AcceptedAtUtc { get; set; }

    public Guid? AcceptedByStaffUserId { get; set; }

    public string? AcceptedByName { get; set; }
}
