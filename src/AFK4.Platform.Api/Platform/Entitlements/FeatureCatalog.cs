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
        new(PlatformFeatureNames.Loyalty, "Лояльность и кэшбек",
            "Начисление бонусов игрокам за игру и покупки.", EnabledByDefault: true),
        new(PlatformFeatureNames.OnlineTopUp, "Онлайн-пополнение",
            "Пополнение кошелька банковской картой.", EnabledByDefault: true),
        new(PlatformFeatureNames.PlayerShop, "Магазин и заказы игрока",
            "Заказ еды и товаров с игрового места.", EnabledByDefault: true),
        new(PlatformFeatureNames.Tournaments, "События и турниры",
            "Расписание событий клуба и запись игрока со взносом с кошелька.", EnabledByDefault: true),
        // Выключена по умолчанию: платный тариф рекламы не показывает, включает её бесплатный.
        new(PlatformFeatureNames.PlatformAds, "Реклама платформы на ПК",
            "Каждая третья карточка витрины свободного ПК — реклама, которую продаёт AFK4.", EnabledByDefault: false)
    ];
}
