using System;

namespace AFK4.Shared.Contracts.Players;

/// <summary>Прошедший визит: где сидел, сколько пробыл и на сколько наиграл.</summary>
public sealed record PlayerVisitDto(
    Guid SessionId,
    Guid SeatId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    // Пусто — визит ещё не закрыт.
    DateTimeOffset? EndedAtUtc,
    long TimeChargeMinorUnits,
    long PosTotalMinorUnits,
    long GrandTotalMinorUnits,
    string CurrencyCode,
    bool HasReceipt);
