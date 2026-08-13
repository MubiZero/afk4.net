namespace AFK4.Platform.Api.Data;

public sealed class ReservationEntity
{
    public Guid ReservationId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public Guid? SeatId { get; set; }

    // Groups several seats booked together as one logical reservation (drag across rows). Null for a
    // single-seat booking. Members share this id so the group can be shown/managed as a unit.
    public Guid? ReservationGroupId { get; set; }

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

    public int Version { get; set; } = 1;

    public Guid? StartedSessionId { get; set; }

    // Billing choice the player made when booking from the app, and the price the server computed
    // for it. Null on operator-created bookings and on anything created before the app started
    // asking — those are still priced at the desk, so the columns stay nullable.
    //
    // The amount is stored, not recomputed on read: the tariff version can be retired and the price
    // list can change between booking and seating, and the player agreed to this number.
    public Guid? TariffVersionId { get; set; }

    public long? EstimatedCostMinorUnits { get; set; }

    public string? CurrencyCode { get; set; }
}
