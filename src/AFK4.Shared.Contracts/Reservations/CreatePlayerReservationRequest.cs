using System;

namespace AFK4.Shared.Contracts.Reservations;

// Player-initiated reservation request.
// SeatId is optional (unassigned reservation). StartsAtUtc and EndsAtUtc are
// absolute — the service derives DurationMinutes internally.
//
// TariffVersionId carries the player's billing choice made in the app. It is optional so older
// clients keep working, but a booking without it cannot be priced: the hold slice needs the exact
// amount the player agreed to, not a branch-default guess. The package path (booking covered by
// already-purchased time) belongs to that same slice — reserving package minutes is a write on the
// package, not a field on the request.
//
// BranchId — филиал, в который игрок придёт. Нужен только в первом действии в клубе, где счёта
// ещё нет: у сети с несколькими филиалами сервер не гадает, куда записать счёт. У игрока со
// счётом филиал уже известен, и присланный его не переписывает.
public sealed record CreatePlayerReservationRequest(
    Guid? SeatId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Note,
    Guid? TariffVersionId = null,
    Guid? BranchId = null);
