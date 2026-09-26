using AFK4.Shared.Contracts.Showcase;

namespace AFK4.Shared.Contracts.Ads;

/// <summary>
/// Реклама платформы на ПК клуба — глазами клуба (спека рекламы, §8.4): клуб по закону тоже
/// распространитель рекламы и должен видеть, что идёт на его ПК, и уметь ответить проверяющему.
/// </summary>
public sealed record ClubAdsDto(
    // Реклама платформы включена клубу — это бесплатный тариф.
    bool AdsEnabled,
    // Показы считаются за эти дни по UTC, «2026-09-01».
    string From,
    string To,
    IReadOnlyList<ClubAdDto> Ads);

public sealed record ClubAdDto(
    Guid CreativeId,
    string Advertiser,
    // Одно из AdCategoryNames
    string Category,
    // Таджикский текст — первым; русский — по желанию рекламодателя.
    string Title,
    string? Body,
    string? TitleRu,
    string? BodyRu,
    string? ImageUrl,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    // Идёт на ПК клуба сейчас.
    bool Running,
    // Показы и секунды на экране на ПК клуба за окно From–To.
    long Impressions,
    long ShownSeconds,
    string? LastShownDay,
    ShowcaseSellerDto? Seller = null,
    bool RequiresCertification = false,
    DateTimeOffset? OfferUntilUtc = null);

public static class ClubAdsLimits
{
    public const int WindowDays = 30;
}
