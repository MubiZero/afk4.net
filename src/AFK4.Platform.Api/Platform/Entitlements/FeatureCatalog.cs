using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>Фичи, объявленные кодом. Имя и описание — стартовые; дальше их правит панель.</summary>
public static class FeatureCatalog
{
    public sealed record Declaration(string FeatureKey, string Name, string Description, bool EnabledByDefault);

    public static readonly IReadOnlyList<Declaration> Declared =
    [
        new(PlatformFeatureNames.OnlineBooking, "Онлайн-бронирование",
            "Игрок сам бронирует место через личный кабинет.", EnabledByDefault: true),
        new(PlatformFeatureNames.Loyalty, "Лояльность и кэшбэк",
            "Начисление бонусов игрокам за игру и покупки.", EnabledByDefault: true),
        new(PlatformFeatureNames.OnlineTopUp, "Онлайн-пополнение",
            "Пополнение кошелька банковской картой.", EnabledByDefault: true),
        new(PlatformFeatureNames.PlayerShop, "Магазин и заказы игрока",
            "Заказ еды и товаров с игрового места.", EnabledByDefault: true)
    ];
}
