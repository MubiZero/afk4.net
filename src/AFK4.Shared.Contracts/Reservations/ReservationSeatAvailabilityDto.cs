namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Места филиала, на которые бронь в окне [<see cref="StartsAtUtc"/>, <see cref="EndsAtUtc"/>)
/// встанет без конфликта — тем же правилом, которым сервер эту бронь примет или отклонит.
///
/// Окно возвращается вместе с ответом: панель спрашивает его для конкретной брони, и ответ на
/// окно, которое уже сменилось, не должен тихо стать списком для нового.
/// </summary>
public sealed record ReservationSeatAvailabilityDto(
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<Guid> FreeSeatIds);
