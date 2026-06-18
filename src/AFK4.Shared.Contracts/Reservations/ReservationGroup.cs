namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Books several seats as one logical reservation (drag across timeline rows). All-or-nothing:
/// if any seat conflicts with an existing reservation or session, the whole group is rejected and
/// the conflicting seats are reported back so the operator can adjust the selection.
/// </summary>
public sealed record CreateReservationGroupRequest(
    Guid OrganizationId,
    Guid? PlayerAccountId,
    IReadOnlyList<Guid> SeatIds,
    string CustomerName,
    string? PhoneNumber,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    string Source,
    string? Note);

public sealed record ReservationGroupConflictDto(
    Guid SeatId,
    string Reason);

/// <summary>
/// Group-create outcome. On success <see cref="ReservationGroupId"/> and <see cref="Reservations"/>
/// are populated and <see cref="Conflicts"/> is empty; on conflict it is the other way round.
/// </summary>
public sealed record ReservationGroupResultDto(
    Guid? ReservationGroupId,
    IReadOnlyList<ReservationDto> Reservations,
    IReadOnlyList<ReservationGroupConflictDto> Conflicts);
