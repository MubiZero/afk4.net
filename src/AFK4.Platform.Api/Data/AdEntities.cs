namespace AFK4.Platform.Api.Data;

/// <summary>Рекламодатель платформы. Контакт — для менеджера AFK4, наружу не уходит.</summary>
public sealed class AdAdvertiserEntity
{
    public Guid AdvertiserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Contact { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid? CreatedByPlatformAdminUserId { get; set; }
}

/// <summary>Кампания: кого, где и когда показывать. Нацеливание — только город и клуб, не игрок.</summary>
public sealed class AdCampaignEntity
{
    public Guid CampaignId { get; set; }

    public Guid AdvertiserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary><see cref="AFK4.Shared.Contracts.Ads.AdCategoryNames"/>.</summary>
    public string Category { get; set; } = string.Empty;

    public DateTimeOffset StartsAtUtc { get; set; }

    public DateTimeOffset EndsAtUtc { get; set; }

    /// <summary>Города JSON-массивом; пустой — все города.</summary>
    public string CitiesJson { get; set; } = "[]";

    /// <summary>Клубы JSON-массивом; пустой — все клубы.</summary>
    public string OrganizationIdsJson { get; set; } = "[]";

    /// <summary><see cref="AFK4.Shared.Contracts.Ads.AdCampaignStateNames"/>.</summary>
    public string State { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? UpdatedByPlatformAdminUserId { get; set; }
}

/// <summary>Креатив кампании: то, что видно на экране. Показывается только одобренный.</summary>
public sealed class AdCreativeEntity
{
    public Guid CreativeId { get; set; }

    public Guid CampaignId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary><see cref="AFK4.Shared.Contracts.Ads.AdModerationNames"/>.</summary>
    public string Moderation { get; set; } = string.Empty;

    public string? RejectedReason { get; set; }

    public DateTimeOffset? ModeratedAtUtc { get; set; }

    public Guid? ModeratedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>Показы креатива в филиале за день — только суммы, строки по игроку нет.</summary>
public sealed class AdImpressionDailyEntity
{
    public Guid AdImpressionDailyId { get; set; }

    public Guid CreativeId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public DateOnly Day { get; set; }

    public long Impressions { get; set; }

    public long ShownMs { get; set; }
}

/// <summary>Принятая пачка показов с ПК: повтор той же пачки не удваивает счёт.</summary>
public sealed class AdImpressionBatchEntity
{
    public Guid OrganizationId { get; set; }

    public Guid DeviceId { get; set; }

    public string BatchId { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
