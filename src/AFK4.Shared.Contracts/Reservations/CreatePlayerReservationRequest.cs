using System;

namespace AFK4.Shared.Contracts.Reservations;

// Player-initiated reservation request.
// SeatId is optional (unassigned reservation). StartsAtUtc and EndsAtUtc are
// absolute — the service derives DurationMinutes internally.
public sealed record CreatePlayerReservationRequest(
    Guid? SeatId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Note);
