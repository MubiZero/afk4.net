using System.Globalization;

namespace AFK4.Shared.Contracts.Ads;

/// <summary>
/// Реклама платформы в витрине свободного ПК (спека `2026-09-25-platform-ads-design.md`). Продаёт
/// её AFK4, показывается она только клубам с фичей <c>platform_ads</c> — это бесплатный тариф.
/// </summary>
public sealed record AdvertiserDto(Guid AdvertiserId, string Name, string Contact, DateTimeOffset CreatedAtUtc);

public sealed record UpsertAdvertiserRequest(string Name, string? Contact);

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
    DateTimeOffset UpdatedAtUtc);

public sealed record UpsertAdCampaignRequest(
    Guid AdvertiserId,
    string Name,
    string Category,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<string>? Cities,
    IReadOnlyList<Guid>? OrganizationIds);

public sealed record SetAdCampaignStateRequest(
    // Одно из AdCampaignStateNames
    string State);

public sealed record AdCreativeDto(
    Guid CreativeId,
    Guid CampaignId,
    string Title,
    string? Body,
    string? ImageUrl,
    // Одно из AdModerationNames
    string Moderation,
    string? RejectedReason,
    DateTimeOffset? ModeratedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record UpsertAdCreativeRequest(string Title, string? Body, string? ImageUrl);

public sealed record ModerateAdCreativeRequest(
    bool Approve,
    // Причина отказа — рекламодателю через менеджера платформы. Обязательна при отказе.
    string? Reason,
    // Модератор подтверждает то, чего код не проверит: это не другой клуб, не алкоголь, не табак и
    // не ставки. Без отметки одобрить нельзя.
    bool ConfirmedAllowed);

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

    // Алкоголя, табака, ставок и клубов здесь нет вовсе: выбрать их нельзя (PRD).
    public static readonly IReadOnlyList<string> All = [Food, Electronics, Games, Education, Services, Telecom, Other];
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
}
