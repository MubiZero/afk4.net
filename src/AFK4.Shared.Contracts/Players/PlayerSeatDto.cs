namespace AFK4.Shared.Contracts.Players;

// Место в зале глазами игрока: как называется, где стоит и свободно ли.
//
// Идентификатора устройства здесь больше нет: сессия начинается кодом с монитора, а не выбором из
// списка. Список остался витриной — «есть ли вообще куда сесть», — и занятое место в нём тоже
// нужно: «PC-07 занят» это ответ, а исчезнувшее место выглядит сбоем приложения.
public sealed record PlayerSeatDto(
    Guid SeatId,
    string SeatName,
    string ZoneName,
    bool IsAvailable,
    // Почему занято: "session" — за ним играют, "reservation" — забронировано на ближайшее время,
    // "offline" — компьютер не на связи. null, когда место свободно.
    string? UnavailableReason);

/// <summary>Почему за место нельзя сесть. Значения уходят в приложение как есть.</summary>
public static class PlayerSeatUnavailableReasons
{
    public const string Session = "session";
    public const string Reservation = "reservation";
    public const string Offline = "offline";
}
