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
    DateTimeOffset? GatewayExpiresAtUtc = null,
    string? Qr = null,
    string? DeepLink = null);

/// <summary>
/// Чем клуб принимает деньги прямо сейчас. Стойка — всегда: это наличные в кассе. Онлайн держится
/// на двух вещах сразу — тариф платформы разрешает и у клуба заведён мерчант банка, — и приложение
/// обязано узнать это до того, как предложит человеку кнопку.
/// </summary>
public sealed record PlayerTopUpMethodsDto(bool Counter, bool Online);
