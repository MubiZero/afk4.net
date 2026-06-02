namespace AFK4.Platform.Api.Data;

public sealed class BranchEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public bool RequireManualDeviceApproval { get; set; }

    // Per-branch default ceiling for an open postpaid tab; null means unbounded.
    public long? PostpaidCreditLimitMinorUnits { get; set; }

    // Per-branch offline grace window (minutes) overriding the global SessionLeaseOptions default;
    // null means use the global default. Resolved + clamped to [1,120] by GraceLeasePolicy.
    public int? GraceLeaseMinutes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
