using System;

namespace AFK4.Shared.Contracts.Reservations;

// What a booking will cost before the player commits to it.
//
// The price is asked of the server rather than computed in the app on purpose: the minimum-billable
// floor and the rounding increment are billing rules, and a second implementation in the client
// would quietly disagree with the charge the moment either side changed.
public sealed record ReservationQuoteRequest(
    Guid TariffVersionId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);

// BillableMinutes can exceed the booked span — that is the point of showing it: an hour booked on a
// tariff with a two-hour minimum is billed as two hours, and the player sees why before booking.
public sealed record ReservationQuoteDto(
    Guid TariffVersionId,
    string TariffName,
    int RequestedMinutes,
    int BillableMinutes,
    long AmountMinorUnits,
    string CurrencyCode);
