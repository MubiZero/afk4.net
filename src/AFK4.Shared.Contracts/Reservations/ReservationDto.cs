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
    Guid? StartedSessionId = null,
    // Billing choice carried by a self-service booking, and the price the server computed for it.
    // Null for desk-created bookings — those are still priced when the player is seated.
    Guid? TariffVersionId = null,
    string? TariffName = null,
    long? EstimatedCostMinorUnits = null,
    string? CurrencyCode = null,
    // Докуда клуб обещал ответить на заявку и когда ответил. Срок есть только у заявки, которая
    // ждёт решения стойки; подтверждённую бронь по таймеру никто не снимает.
    DateTimeOffset? RespondByUtc = null,
    DateTimeOffset? ConfirmedAtUtc = null);

public sealed record ReservationSearchResultDto(
    IReadOnlyList<ReservationDto> Reservations,
    int Limit);
