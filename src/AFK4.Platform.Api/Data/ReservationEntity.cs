namespace AFK4.Platform.Api.Data;

public sealed class ReservationEntity
{
    public Guid ReservationId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public Guid? SeatId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }

    public DateTimeOffset EndsAtUtc { get; set; }

    public string State { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public Guid CreatedByStaffUserId { get; set; }

    public Guid? UpdatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string CancelReason { get; set; } = string.Empty;

    public DateTimeOffset? SeatedAtUtc { get; set; }
}
