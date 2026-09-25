using System.Globalization;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Showcase;

/// <summary>
/// Витрина свободного ПК (спека оболочки, §5.7): что показывает экран, пока за ПК никто не сидит.
/// Тексты — словами клуба, как их написали в Панели; подписи вроде «Турнир» переводит оболочка.
/// </summary>
public sealed record DeviceShowcaseDto(IReadOnlyList<ShowcaseCardDto> Cards);

public sealed record ShowcaseCardDto(
    // Стабильный ключ карточки: «news:…», «tariff:…». По нему агент узнаёт карточку между
    // обновлениями, а экран не перезапускает показ, когда список не изменился.
    string CardId,
    // Одно из ShowcaseCardKindNames
    string Kind,
    // У «Пакетов» заголовок пуст: его пишет оболочка на языке экрана.
    string Title,
    string? Body = null,
    // Короткая строка рядом с видом карточки: дисциплина турнира («Dota 2»).
    string? Subtitle = null,
    // Адрес картинки. С сервера — адрес в медиа-хранилище, на экран — адрес в кэше ПК: чужих
    // адресов экран не получает.
    string? ImageUrl = null,
    // Цена: час тарифа, товар, взнос турнира. Пусто — цены у карточки нет (или взнос бесплатный).
    MoneyDto? Price = null,
    // Часы тарифа по времени клуба, «22:00–06:00». Пусто — круглые сутки.
    string? TimeWindow = null,
    // Начало турнира.
    DateTimeOffset? StartsAtUtc = null,
    // Строки карточки «Пакеты».
    IReadOnlyList<ShowcasePackageLineDto>? Packages = null);

public sealed record ShowcasePackageLineDto(string Name, MoneyDto Price, int Minutes);

public static class ShowcaseCardKindNames
{
    public const string News = "news";

    public const string Tariff = "tariff";

    public const string Product = "product";

    public const string Tournament = "tournament";

    public const string Packages = "packages";

    public const string BarHit = "bar_hit";
}

public static class ShowcaseLimits
{
    public const int MaxCards = 14;

    public const int MaxNews = 6;

    public const int MaxTariffs = 3;

    public const int MaxProducts = 4;

    public const int MaxPackages = 4;

    // Экран крупный: длинная новость целиком не влезет, а на ПК её дочитают в приложении.
    public const int MaxBodyLength = 280;

    public const int TournamentHorizonDays = 14;

    public const int BarHitWindowDays = 30;

    // Одна продажа за месяц — не хит, а случайность.
    public const int BarHitMinimumUnits = 3;
}

public static class ShowcaseRoutes
{
    public static string Device(Guid deviceId, Guid organizationId, Guid branchId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/devices/{deviceId:D}/showcase?organizationId={organizationId:D}&branchId={branchId:D}");
}
