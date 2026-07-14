namespace AFK4.Shared.Contracts.Reservations;

public sealed record ReservationDto(
    Guid ReservationId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? PlayerAccountId,
    Guid? SeatId,
    string? SeatName,
    string? ZoneName,
    string CustomerName,
    string? PhoneNumber,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    int DurationMinutes,
    string State,
    string Source,
    string Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string CancelReason,
    Guid? ReservationGroupId,
    int Version = 1,
    Guid? StartedSessionId = null);

public sealed record ReservationSearchResultDto(
    IReadOnlyList<ReservationDto> Reservations,
    int Limit);
