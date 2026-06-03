using System;

namespace AFK4.Shared.Contracts.Reservations;

// Player-facing reservation view — no staff-only fields (no CustomerName separate from
// context, no CreatedByStaffUserId, no UpdatedBy, no ZoneName leak).
public sealed record PlayerReservationDto(
    Guid ReservationId,
    Guid? SeatId,
    string? SeatName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string State,
    string? Note);
