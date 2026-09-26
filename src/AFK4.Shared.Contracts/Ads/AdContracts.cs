using System.Globalization;

namespace AFK4.Shared.Contracts.Ads;

/// <summary>
/// Реклама платформы в витрине свободного ПК (спека `2026-09-25-platform-ads-design.md`). Продаёт
/// её AFK4, показывается она только клубам с фичей <c>platform_ads</c> — это бесплатный тариф.
/// </summary>
public sealed record AdvertiserDto(
    Guid AdvertiserId,
    // Имя на карточке: «Реклама · {Name}».
    string Name,
    string Contact,
    DateTimeOffset CreatedAtUtc,
    // Реквизиты для договора и рекламы с продажей на расстоянии (закон РТ «О рекламе», ст. 14(1)):
    // наименование, ИНН (единый идентификационный номер) и место нахождения.
    string LegalName = "",
    string TaxId = "",
    string Address = "");

public sealed record UpsertAdvertiserRequest(string Name, string? Contact, string? LegalName = null, string? TaxId = null, string? Address = null);

public sealed record AdCampaignDto(
    Guid CampaignId,
    Guid AdvertiserId,
    string AdvertiserName,
    string Name,
    // Одно из AdCategoryNames
    string Category,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    // Пусто — все города.
    IReadOnlyList<string> Cities,
    // Пусто — все клубы.
    IReadOnlyList<Guid> OrganizationIds,
    // Одно из AdCampaignStateNames
    string State,
    IReadOnlyList<AdCreativeDto> Creatives,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    AdCampaignComplianceDto? Compliance = null);

/// <summary>
/// Что кампания обязана сказать на карточке по закону РТ «О рекламе» (спека рекламы, §8):
/// номер разрешения Минздрава, продажа на расстоянии, обязательная сертификация, условия сделки.
/// </summary>
public sealed record AdCampaignComplianceDto(
    // Разрешение или лицензия Минздрава — обязательно для «Здоровья и красоты» (ст. 17).
    string? PermitNumber = null,
    // Продажа на расстоянии: карточка печатает наименование, ИНН и адрес продавца (ст. 14(1)).
    bool DistanceSelling = false,
    // Товар подлежит обязательной сертификации: карточка печатает пометку (ст. 5).
    bool RequiresCertification = false,
    // В рекламе цена или условия сделки: карточка печатает срок предложения — конец кампании (ст. 26).
    bool ContainsOffer = false);

public sealed record UpsertAdCampaignRequest(
    Guid AdvertiserId,
    string Name,
    string Category,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<string>? Cities,
    IReadOnlyList<Guid>? OrganizationIds,
    AdCampaignComplianceDto? Compliance = null);

public sealed record SetAdCampaignStateRequest(
    // Одно из AdCampaignStateNames
    string State);

public sealed record AdCreativeDto(
    Guid CreativeId,
    Guid CampaignId,
    // Заголовок и текст на государственном языке — таджикском (ст. 5 закона о рекламе, закон о
    // госязыке): обязательны и идут на карточке первыми.
    string Title,
    string? Body,
    string? ImageUrl,
    // Одно из AdModerationNames
    string Moderation,
    string? RejectedReason,
    DateTimeOffset? ModeratedAtUtc,
    DateTimeOffset CreatedAtUtc,
    // Русский — второй строкой по желанию рекламодателя.
    string? TitleRu = null,
    string? BodyRu = null,
    // Слова, которые закон разрешает только с документом («лучший», «№ 1», ст. 7): модератору —
    // подсказка, а не запрет.
    IReadOnlyList<string>? WordingFlags = null,
    // Снят с показа. Показанный креатив не правится и не удаляется — его хранят год (ст. 22).
    DateTimeOffset? ArchivedAtUtc = null);

public sealed record UpsertAdCreativeRequest(string Title, string? Body, string? ImageUrl, string? TitleRu = null, string? BodyRu = null);

public sealed record ModerateAdCreativeRequest(
    bool Approve,
    // Причина отказа — рекламодателю через менеджера платформы. Обязательна при отказе.
    string? Reason,
    // Модератор подтверждает то, чего код не проверит, — каждую строку AdModerationCheckNames.
    // Без всех отметок одобрить нельзя.
    IReadOnlyList<string>? Confirmed = null);

/// <summary>Отметки модератора при одобрении — по статьям закона РТ «О рекламе» (спека рекламы, §8.2).</summary>
public static class AdModerationCheckNames
{
    /// <summary>Не другой клуб, не ставки и не казино — правило платформы.</summary>
    public const string NotClubOrBetting = "not_club_or_betting";

    /// <summary>Нет запрещённого товара (ст. 17), рекламодатель не производит алкоголь и табак (ст. 20).</summary>
    public const string NoBannedGoods = "no_banned_goods";

    /// <summary>Защита несовершеннолетних (ст. 21).</summary>
    public const string Minors = "minors";

    /// <summary>Достоверно: превосходные степени — только с документом (ст. 7).</summary>
    public const string Truthful = "truthful";

    /// <summary>Этично и честно: без оскорблений, порочащих сравнений, скрытых вставок (ст. 6, 8, 9, 10).</summary>
    public const string Ethical = "ethical";

    /// <summary>Текст на картинке — на таджикском или есть и на таджикском (ст. 5).</summary>
    public const string TajikOnImage = "tajik_on_image";

    /// <summary>
    /// Только у кампаний «Финансы» (ст. 18): без обещаний доходности и гарантий, без умолчания
    /// условий договора. В <see cref="All"/> не входит — у остальных категорий её нет.
    /// </summary>
    public const string FinanceTerms = "finance_terms";

    /// <summary>Отметки, которые нужны любой кампании.</summary>
    public static readonly IReadOnlyList<string> All = [NotClubOrBetting, NoBannedGoods, Minors, Truthful, Ethical, TajikOnImage];

    public static IReadOnlyList<string> RequiredFor(string category) =>
        category == AdCategoryNames.Finance ? [.. All, FinanceTerms] : All;
}

/// <summary>Строка отчёта показов: креатив в филиале за день. Игрока в строке нет и быть не может.</summary>
public sealed record AdImpressionRowDto(
    string Day,
    Guid CampaignId,
    string CampaignName,
    Guid CreativeId,
    string CreativeTitle,
    Guid OrganizationId,
    string OrganizationName,
    Guid BranchId,
    string BranchName,
    string City,
    long Impressions,
    long ShownSeconds);

/// <summary>Пачка показов с ПК: суммы по карточке за день.</summary>
public sealed record DeviceShowcaseImpressionsRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    // Ключ пачки: повтор той же пачки после обрыва связи не удваивает счёт.
    string BatchId,
    IReadOnlyList<ShowcaseImpressionDto> Items);

public sealed record ShowcaseImpressionDto(
    string CardId,
    // День показа по UTC, «2026-09-25».
    string Day,
    int Impressions,
    long ShownMs);

public static class AdCategoryNames
{
    public const string Food = "food";

    public const string Electronics = "electronics";

    public const string Games = "games";

    public const string Education = "education";

    public const string Services = "services";

    public const string Telecom = "telecom";

    public const string Other = "other";

    /// <summary>Лекарства без рецепта, медтехника, БАД, косметика, методы лечения — только с разрешением Минздрава (ст. 17).</summary>
    public const string HealthBeauty = "health_beauty";

    /// <summary>Банки, страхование, инвестиции — без обещаний доходности (ст. 18).</summary>
    public const string Finance = "finance";

    /// <summary>Социальная реклама — без брендов (ст. 19); считается отдельно.</summary>
    public const string Social = "social";

    // Алкоголя, табака, ставок, клубов и прочего запрещённого ст. 17 здесь нет вовсе: выбрать их нельзя.
    public static readonly IReadOnlyList<string> All = [Food, Electronics, Games, Education, Services, Telecom, HealthBeauty, Finance, Social, Other];
}

public static class AdCampaignStateNames
{
    public const string Draft = "draft";

    /// <summary>Идёт в своих датах, если у неё есть одобренный креатив.</summary>
    public const string Active = "active";

    public const string Paused = "paused";

    public static readonly IReadOnlyList<string> All = [Draft, Active, Paused];
}

public static class AdModerationNames
{
    public const string Pending = "pending";

    public const string Approved = "approved";

    public const string Rejected = "rejected";
}

public static class AdErrorCodeNames
{
    public const string Invalid = "ad_invalid";

    public const string NotApproved = "ad_campaign_without_approved_creative";

    public const string ConfirmationRequired = "ad_moderation_confirmation_required";

    /// <summary>Для «Здоровья и красоты» нужен номер разрешения Минздрава.</summary>
    public const string PermitRequired = "ad_permit_required";

    /// <summary>Одобренный креатив не правится: его хранят как показанный. Нужен новый креатив.</summary>
    public const string CreativeLocked = "ad_creative_locked";

    /// <summary>Картинку не удалось скачать для хранения — одобрить без копии нельзя.</summary>
    public const string ImageUnavailable = "ad_image_unavailable";
}

public static class AdLimits
{
    public const int NameMax = 160;

    public const int ContactMax = 400;

    public const int TitleMax = 120;

    public const int BodyMax = 280;

    public const int ImageUrlMax = 2048;

    public const int MaxCities = 50;

    public const int MaxOrganizations = 200;

    public const int ReasonMax = 400;

    public const int LegalNameMax = 200;

    public const int AddressMax = 300;

    // ИНН в Таджикистане — девять цифр, единый идентификационный номер — десять; запас на иностранных.
    public const int TaxIdMinDigits = 9;

    public const int TaxIdMaxDigits = 14;

    public const int PermitMax = 120;

    // Копия картинки, которую сервер хранит год (ст. 22) и отдаёт ПК вместо чужого адреса.
    public const int ImageMaxBytes = 2 * 1024 * 1024;

    // Больше в пачке с одного ПК за час не бывает: карточка стоит 9 секунд.
    public const int MaxBatchItems = 500;

    public const int MaxImpressionsPerItem = 10_000;

    public const int ReportMaxDays = 92;
}

public static class AdRoutes
{
    public const string Advertisers = "/api/platform/ads/advertisers";

    public const string Campaigns = "/api/platform/ads/campaigns";

    public const string Report = "/api/platform/ads/report";

    public static string DeviceImpressions(Guid deviceId) => string.Create(
        CultureInfo.InvariantCulture, $"/api/devices/{deviceId:D}/showcase/impressions");

    /// <summary>Хранимая копия картинки одобренного креатива — её и видит ПК.</summary>
    public static string CreativeImage(Guid creativeId) => string.Create(
        CultureInfo.InvariantCulture, $"/api/showcase/ad-images/{creativeId:N}");
}
