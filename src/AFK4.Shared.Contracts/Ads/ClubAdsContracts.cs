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
    DateTimeOffset? OfferUntilUtc = null,
    // Клуб уже пожаловался, и платформа ещё не ответила.
    bool ComplaintOpen = false,
    // Ответ платформы на последнюю закрытую жалобу клуба на эту рекламу.
    string? ComplaintAnswer = null);

public static class ClubAdsLimits
{
    public const int WindowDays = 30;
}

/// <summary>
/// Жалоба клуба на рекламу на его ПК (спека рекламы, §8.4): клуб — распространитель, но снять
/// рекламу сам не может, поэтому сообщает платформе, а та решает — снять креатив или нет.
/// </summary>
public sealed record ReportClubAdRequest(
    // Одно из AdComplaintReasonNames
    string Reason,
    string? Comment);

public sealed record AdComplaintDto(
    Guid ComplaintId,
    Guid OrganizationId,
    string OrganizationName,
    Guid CampaignId,
    string CampaignName,
    Guid CreativeId,
    string CreativeTitle,
    string Advertiser,
    // Одно из AdComplaintReasonNames
    string Reason,
    string? Comment,
    string ReportedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string? Resolution,
    // Креатив уже снят с показа.
    bool CreativeArchived);

public sealed record ResolveAdComplaintRequest(string Resolution);

public static class AdComplaintReasonNames
{
    /// <summary>Товар, запрещённый законом (ст. 17).</summary>
    public const string BannedGoods = "banned_goods";

    /// <summary>Не подходит детям и подросткам (ст. 21).</summary>
    public const string Minors = "minors";

    /// <summary>Неправда или обман (ст. 7, 9).</summary>
    public const string Misleading = "misleading";

    /// <summary>Реклама другого клуба.</summary>
    public const string OtherClub = "other_club";

    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [BannedGoods, Minors, Misleading, OtherClub, Other];
}

public static class AdComplaintLimits
{
    public const int CommentMax = 500;

    public const int ResolutionMax = 500;
}

public static class AdComplaintErrorCodeNames
{
    /// <summary>Жалоба на рекламу, которой на ПК клуба не было.</summary>
    public const string NotShown = "ad_complaint_not_shown";
}
