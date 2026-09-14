namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Перенос собственной брони игроком: новое время и, если нужно, другое место.
///
/// Длительности здесь нет намеренно. «Перенести» — это то же самое на другое время; изменить
/// длину — другое решение с другой ценой, и прятать его в ту же кнопку значит однажды удивить
/// человека суммой.
/// </summary>
public sealed record MovePlayerReservationRequest(
    DateTimeOffset StartsAtUtc,
    Guid? SeatId = null,
    int? ExpectedVersion = null);
