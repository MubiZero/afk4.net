using System;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerVisitDto(
    Guid SessionId,
    Guid SeatId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long TimeChargeMinorUnits,
    long PosTotalMinorUnits,
    long GrandTotalMinorUnits,
    string CurrencyCode,
    bool HasReceipt);
