using System;

namespace AFK4.Shared.Contracts.Reservations;

// What a booking will cost before the player commits to it.
//
// The price is asked of the server rather than computed in the app on purpose: the minimum-billable
// floor and the rounding increment are billing rules, and a second implementation in the client
// would quietly disagree with the charge the moment either side changed.
//
// SeatCount умножает цену на число мест на стороне сервера. Умножение сложным не выглядит, но
// показанная и списанная суммы обязаны приходить из одного места: считаясь порознь, они однажды
// разойдутся — а это та цена, на которую игрок согласился.
public sealed record ReservationQuoteRequest(
    Guid TariffVersionId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    int SeatCount = 1);

// BillableMinutes can exceed the booked span — that is the point of showing it: an hour booked on a
// tariff with a two-hour minimum is billed as two hours, and the player sees why before booking.
//
// AmountMinorUnits — сумма за ВСЮ бронь, включая все её места. Отдавать цену одного места и
// оставлять умножение приложению значило бы показывать не то число, которое будет заморожено.
public sealed record ReservationQuoteDto(
    Guid TariffVersionId,
    string TariffName,
    int RequestedMinutes,
    int BillableMinutes,
    long AmountMinorUnits,
    string CurrencyCode,
    int SeatCount = 1);
