using System;
using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Players;

/// <summary>Покупка в баре: когда, что и на сколько.</summary>
public sealed record PlayerPurchaseDto(
    Guid PosSaleId,
    DateTimeOffset CreatedAtUtc,
    long TotalMinorUnits,
    string CurrencyCode,
    IReadOnlyList<PlayerPurchaseLineDto> Lines);
