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
    string CurrencyCode,
    // По какой цене идёт счёт и где человек сидит. Растущая сумма без ставки — это число, которое
    // нечем проверить: видно, что платишь, и не видно, за что. Цена — за час, а не за минуту:
    // клуб продаёт часы, и в них же человек считает.
    //
    // Пусто там, где тарифа у сессии нет вовсе (гостевая, заведённая на стойке руками) — врать
    // подставленной ставкой хуже, чем честно промолчать.
    string? TariffName = null,
    long? PricePerHourMinorUnits = null,
    string? ZoneName = null);
