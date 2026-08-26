namespace AFK4.Shared.Contracts.Platform.Features;

/// <summary>
/// Ключи фич, которые платформа умеет включать и выключать клубу. Каждый ключ обязан иметь
/// точку проверки в коде: флаг без потребителя — мусор, который невозможно опознать через месяц.
/// </summary>
public static class PlatformFeatureNames
{
    /// <summary>Код отказа, когда фича выключена. Фразу собирает клиент.</summary>
    public const string DisabledCode = "feature_disabled";

    public const string OnlineBooking = "online_booking";

    public const string Loyalty = "loyalty";

    public const string OnlineTopUp = "online_topup";

    public const string PlayerShop = "player_shop";

    public const string Tournaments = "tournaments";

    public static readonly IReadOnlyList<string> All =
        [OnlineBooking, Loyalty, OnlineTopUp, PlayerShop, Tournaments];
}
