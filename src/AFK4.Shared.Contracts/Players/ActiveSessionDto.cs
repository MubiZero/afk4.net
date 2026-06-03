using System;

namespace AFK4.Shared.Contracts.Players;

public sealed record ActiveSessionDto(
    Guid SessionId,
    Guid SeatId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    string DurationMode,            // "open" | "fixed"
    int? RemainingSeconds,          // fixed only
    long? AccruedCostMinorUnits,    // open only
    string CurrencyCode);
