namespace AFK4.Platform.Api.Data;

public sealed class SessionEntity
{
    public Guid SessionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SeatId { get; set; }

    public Guid DeviceId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public string PlayerKind { get; set; } = "guest";

    public Guid? PlayerAccountId { get; set; }

    public string TariffRuleVersionId { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? EndsAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public Guid? CurrentLeaseId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
