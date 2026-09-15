using System;

namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// Заявка на пополнение кошелька: игрок просит зачислить сумму, клуб подтверждает.
/// </summary>
public sealed record PlayerTopUpIntentDto(
    Guid PaymentIntentId,
    long AmountMinorUnits,
    string CurrencyCode,
    string State,
    string Purpose,
    // `counter` — деньги вносят на стойке, `eskhata` — платят из приложения банка.
    string Method,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FulfilledAtUtc,
    bool IsExpired,
    // Страница оплаты в браузере — запасной путь для телефона без приложения банка.
    string? PayUrl = null,
    string? Comment = null,
    DateTimeOffset? GatewayExpiresAtUtc = null,
    string? Qr = null,
    // Ссылка, открывающая приложение банка. Пусто, если платят на стойке или банк её не дал.
    string? DeepLink = null);

/// <summary>
/// Чем клуб принимает деньги прямо сейчас. Стойка — всегда: это наличные в кассе. Онлайн держится
/// на двух вещах сразу — тариф платформы разрешает и у клуба заведён мерчант банка, — и приложение
/// обязано узнать это до того, как предложит человеку кнопку.
/// </summary>
public sealed record PlayerTopUpMethodsDto(bool Counter, bool Online);
