using System;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerTopUpIntentDto(
    Guid PaymentIntentId,
    long AmountMinorUnits,
    string CurrencyCode,
    string State,
    string Purpose,
    string Method,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FulfilledAtUtc,
    bool IsExpired,
    string? PayUrl = null,
    string? Comment = null,
    DateTimeOffset? GatewayExpiresAtUtc = null);
