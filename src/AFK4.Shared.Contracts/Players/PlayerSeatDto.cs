namespace AFK4.Shared.Contracts.Players;

// Место в зале глазами игрока, который выбирает, куда сесть.
//
// DeviceId нужен для старта сессии, но сам по себе игроку ничего не говорит — на экране он видит
// имя места и зал. Занятое место остаётся в списке: «PC-07 занят» — это ответ, а исчезнувшее место
// выглядит как сбой приложения.
public sealed record PlayerSeatDto(
    Guid SeatId,
    Guid DeviceId,
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
