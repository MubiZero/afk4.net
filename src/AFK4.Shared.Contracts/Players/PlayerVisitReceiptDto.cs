using System;
using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Players;

/// <summary>Чек визита: время, покупки и итог.</summary>
public sealed record PlayerVisitReceiptDto(
    string ReceiptNumber,
    DateTimeOffset CreatedAtUtc,
    Guid SessionId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long TimeChargeMinorUnits,
    IReadOnlyList<PlayerPurchaseLineDto> PosLines,
    long PosTotalMinorUnits,
    long GrandTotalMinorUnits,
    string CurrencyCode);
