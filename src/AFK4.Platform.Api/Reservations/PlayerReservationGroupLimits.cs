namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Границы брони на компанию.
///
/// Верхняя граница не про вместимость клуба — её проверяет не эта величина, — а про то, что каждое
/// место группы замораживает деньги отдельной записью журнала. Без потолка одна кнопка в приложении
/// умеет разом заморозить весь кошелёк и наплодить строк, а компания больше восьми человек в
/// киберклубе договаривается голосом, а не через форму.
/// </summary>
internal static class PlayerReservationGroupLimits
{
    public const int MinSeats = 1;

    public const int MaxSeats = 8;

    /// <summary>Код отказа для интерфейсов: они переводят его сами.</summary>
    public const string InvalidSeatCountCode = "invalid_seat_count";

    public static bool IsAllowedSeatCount(int seatCount) =>
        seatCount is >= MinSeats and <= MaxSeats;
}
