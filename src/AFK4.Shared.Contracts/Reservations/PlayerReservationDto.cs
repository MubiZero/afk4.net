using System;

namespace AFK4.Shared.Contracts.Reservations;

// Player-facing reservation view — no staff-only fields (no CustomerName separate from
// context, no CreatedByStaffUserId, no UpdatedBy, no ZoneName leak).
//
// The tariff and the estimated cost are the player's own choice priced by the server: the app must
// never re-derive the amount from the price list, because the minimum-billable and rounding rules
// live in billing and would drift the moment either side changed.
public sealed record PlayerReservationDto(
    Guid ReservationId,
    Guid? SeatId,
    string? SeatName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string State,
    string? Note,
    Guid? TariffVersionId = null,
    string? TariffName = null,
    long? EstimatedCostMinorUnits = null,
    string? CurrencyCode = null,
    // Бронь на компанию: у всех мест группы он общий. Без него приложение показало бы компанию из
    // четырёх человек четырьмя одинаковыми строками, между которыми не видно разницы.
    Guid? ReservationGroupId = null);
