using System.Globalization;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Media;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Showcase;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Ads;

/// <summary>
/// Реклама платформы (спека `2026-09-25-platform-ads-design.md`): проверки кампаний и креативов,
/// подбор рекламы для филиала и приём показов с ПК. Правила PRD держит код: запрещённых
/// категорий нет в списке, одобрить креатив без подтверждения модератора нельзя, у показа нет
/// игрока.
/// </summary>
public static class PlatformAds
{
    public const string AdCardPrefix = "ad:";

    public static string? Validate(UpsertAdvertiserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > AdLimits.NameMax)
            return $"Advertiser name is required and at most {AdLimits.NameMax} characters.";
        return request.Contact?.Length > AdLimits.ContactMax ? $"Contact must be at most {AdLimits.ContactMax} characters." : null;
    }

    public static string? Validate(UpsertAdCampaignRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > AdLimits.NameMax)
            return $"Campaign name is required and at most {AdLimits.NameMax} characters.";
        if (!AdCategoryNames.All.Contains(request.Category)) return "Unknown category.";
        if (request.EndsAtUtc <= request.StartsAtUtc) return "The campaign must end after it starts.";
        if ((request.Cities?.Count ?? 0) > AdLimits.MaxCities) return $"At most {AdLimits.MaxCities} cities.";
        if (request.Cities?.Any(string.IsNullOrWhiteSpace) == true) return "A city cannot be empty.";
        return (request.OrganizationIds?.Count ?? 0) > AdLimits.MaxOrganizations ? $"At most {AdLimits.MaxOrganizations} clubs." : null;
    }

    public static string? Validate(UpsertAdCreativeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > AdLimits.TitleMax)
            return $"Title is required and at most {AdLimits.TitleMax} characters.";
        if (request.Body?.Trim().Length > AdLimits.BodyMax) return $"Text must be at most {AdLimits.BodyMax} characters.";
        if (ImageUrlRules.Validate(request.ImageUrl) is { } imageError) return imageError;
        // ПК качает картинки только по https: http-адрес экран не показал бы никогда.
        return request.ImageUrl is { Length: > 0 } url && !url.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "The image must be an https address."
            : null;
    }

    public static IReadOnlyList<string> Cities(AdCampaignEntity campaign) =>
        JsonSerializer.Deserialize<List<string>>(campaign.CitiesJson) ?? [];

    public static IReadOnlyList<Guid> Organizations(AdCampaignEntity campaign) =>
        JsonSerializer.Deserialize<List<Guid>>(campaign.OrganizationIdsJson) ?? [];

    public static void Apply(AdCampaignEntity campaign, UpsertAdCampaignRequest request)
    {
        campaign.AdvertiserId = request.AdvertiserId;
        campaign.Name = request.Name.Trim();
        campaign.Category = request.Category;
        campaign.StartsAtUtc = request.StartsAtUtc;
        campaign.EndsAtUtc = request.EndsAtUtc;
        campaign.CitiesJson = JsonSerializer.Serialize(
            (request.Cities ?? []).Select(city => city.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        campaign.OrganizationIdsJson = JsonSerializer.Serialize((request.OrganizationIds ?? []).Distinct().ToList());
    }

    public static AdCreativeDto ToDto(AdCreativeEntity creative) => new(
        creative.CreativeId, creative.CampaignId, creative.Title, creative.Body, creative.ImageUrl, creative.Moderation,
        creative.RejectedReason, creative.ModeratedAtUtc, creative.CreatedAtUtc);

    public static AdCampaignDto ToDto(AdCampaignEntity campaign, string advertiserName, IEnumerable<AdCreativeEntity> creatives) => new(
        campaign.CampaignId, campaign.AdvertiserId, advertiserName, campaign.Name, campaign.Category,
        campaign.StartsAtUtc, campaign.EndsAtUtc, Cities(campaign), Organizations(campaign), campaign.State,
        creatives.OrderBy(creative => creative.CreatedAtUtc).Select(ToDto).ToList(),
        campaign.CreatedAtUtc, campaign.UpdatedAtUtc);

    public static async Task<IReadOnlyList<AdCampaignDto>> ListAsync(PlatformDbContext db, CancellationToken ct)
    {
        var campaigns = await db.AdCampaigns.AsNoTracking().OrderByDescending(campaign => campaign.StartsAtUtc).ToListAsync(ct);
        var advertisers = await db.AdAdvertisers.AsNoTracking().ToDictionaryAsync(advertiser => advertiser.AdvertiserId, advertiser => advertiser.Name, ct);
        var creatives = (await db.AdCreatives.AsNoTracking().ToListAsync(ct)).ToLookup(creative => creative.CampaignId);
        return campaigns
            .Select(campaign => ToDto(campaign, advertisers.GetValueOrDefault(campaign.AdvertiserId, string.Empty), creatives[campaign.CampaignId]))
            .ToList();
    }

    /// <summary>
    /// Реклама для витрины филиала: одобренные креативы идущих кампаний, нацеленных на его город
    /// или клуб. Порядок — по кругу со сдвигом по филиалу: соседние клубы видят разное.
    /// </summary>
    public static async Task<IReadOnlyList<ShowcaseCardDto>> CardsForBranchAsync(
        PlatformDbContext db, Guid organizationId, Guid branchId, DateTimeOffset now, CancellationToken ct)
    {
        var city = await db.Branches.AsNoTracking()
            .Where(branch => branch.BranchId == branchId && branch.OrganizationId == organizationId)
            .Select(branch => branch.City)
            .SingleOrDefaultAsync(ct) ?? string.Empty;
        var campaigns = (await db.AdCampaigns.AsNoTracking()
                .Where(campaign => campaign.State == AdCampaignStateNames.Active && campaign.StartsAtUtc <= now && campaign.EndsAtUtc > now)
                .ToListAsync(ct))
            .Where(campaign => Targets(campaign, organizationId, city))
            .ToList();
        if (campaigns.Count == 0) return [];

        var ids = campaigns.Select(campaign => campaign.CampaignId).ToList();
        var advertisers = await db.AdAdvertisers.AsNoTracking()
            .Where(advertiser => campaigns.Select(campaign => campaign.AdvertiserId).Contains(advertiser.AdvertiserId))
            .ToDictionaryAsync(advertiser => advertiser.AdvertiserId, advertiser => advertiser.Name, ct);
        var byCampaign = campaigns.ToDictionary(campaign => campaign.CampaignId);
        var cards = (await db.AdCreatives.AsNoTracking()
                .Where(creative => ids.Contains(creative.CampaignId) && creative.Moderation == AdModerationNames.Approved)
                .OrderBy(creative => creative.CreativeId)
                .ToListAsync(ct))
            .Select(creative => new ShowcaseCardDto(
                AdCardPrefix + creative.CreativeId.ToString("N"),
                ShowcaseCardKindNames.Ad,
                creative.Title,
                creative.Body,
                ImageUrl: creative.ImageUrl,
                Advertiser: advertisers.GetValueOrDefault(byCampaign[creative.CampaignId].AdvertiserId, string.Empty)))
            .ToList();
        if (cards.Count < 2) return cards;
        var shift = (int)((uint)branchId.GetHashCode() % (uint)cards.Count);
        return cards.Skip(shift).Concat(cards.Take(shift)).ToList();
    }

    private static bool Targets(AdCampaignEntity campaign, Guid organizationId, string city)
    {
        var organizations = Organizations(campaign);
        if (organizations.Count > 0 && !organizations.Contains(organizationId)) return false;
        var cities = Cities(campaign);
        return cities.Count == 0 || cities.Contains(city.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Реклама — после каждых двух карточек клуба. Меньше двух карточек клуба — одна реклама в конце;
    /// без карточек клуба — не больше трёх реклам.
    /// </summary>
    public static IReadOnlyList<ShowcaseCardDto> Interleave(IReadOnlyList<ShowcaseCardDto> club, IReadOnlyList<ShowcaseCardDto> ads)
    {
        if (ads.Count == 0) return club;
        if (club.Count == 0) return ads.Take(ShowcaseLimits.MaxAdsWithoutClubCards).ToList();
        if (club.Count < ShowcaseLimits.ClubCardsPerAd) return [.. club, ads[0]];

        var result = new List<ShowcaseCardDto>();
        var next = 0;
        for (var index = 0; index < club.Count; index++)
        {
            result.Add(club[index]);
            if ((index + 1) % ShowcaseLimits.ClubCardsPerAd == 0 && next < ads.Count)
            {
                result.Add(ads[next++]);
            }
        }

        return result;
    }

    public enum IngestOutcome { Accepted, Duplicate, Invalid }

    /// <summary>
    /// Пачка показов с ПК. Считаются только рекламные карточки, известные серверу; чужие и
    /// карточки клуба молча пропускаются — счёт клубу не нужен, а ошибка в одной строке не должна
    /// терять всю пачку.
    /// </summary>
    public static async Task<IngestOutcome> IngestAsync(
        PlatformDbContext db, DeviceShowcaseImpressionsRequest request, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BatchId) || request.BatchId.Length > 64 || request.Items.Count > AdLimits.MaxBatchItems)
            return IngestOutcome.Invalid;

        if (await db.AdImpressionBatches.AnyAsync(batch => batch.DeviceId == request.DeviceId && batch.BatchId == request.BatchId, ct))
            return IngestOutcome.Duplicate;

        var parsed = request.Items
            .Select(item => (
                Creative: item.CardId.StartsWith(AdCardPrefix, StringComparison.Ordinal)
                          && Guid.TryParseExact(item.CardId[AdCardPrefix.Length..], "N", out var id) ? id : (Guid?)null,
                Day: DateOnly.TryParseExact(item.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : (DateOnly?)null,
                item.Impressions,
                item.ShownMs))
            .Where(item => item.Creative is not null && item.Day is not null
                && item.Impressions is > 0 and <= AdLimits.MaxImpressionsPerItem && item.ShownMs >= 0
                // Показ из будущего или древний — сбитые часы ПК, а не показ.
                && item.Day <= DateOnly.FromDateTime(now.UtcDateTime.AddDays(1))
                && item.Day >= DateOnly.FromDateTime(now.UtcDateTime.AddDays(-AdLimits.ReportMaxDays)))
            .GroupBy(item => (item.Creative!.Value, item.Day!.Value))
            .Select(group => (group.Key.Item1, group.Key.Item2, Impressions: group.Sum(item => (long)item.Impressions), ShownMs: group.Sum(item => item.ShownMs)))
            .ToList();
        var creativeIds = parsed.Select(item => item.Item1).Distinct().ToList();
        var known = await db.AdCreatives.AsNoTracking()
            .Where(creative => creativeIds.Contains(creative.CreativeId))
            .Select(creative => creative.CreativeId)
            .ToListAsync(ct);

        db.AdImpressionBatches.Add(new AdImpressionBatchEntity
        {
            OrganizationId = request.OrganizationId, DeviceId = request.DeviceId, BatchId = request.BatchId, ReceivedAtUtc = now
        });
        foreach (var (creativeId, day, impressions, shownMs) in parsed.Where(item => known.Contains(item.Item1)))
        {
            var row = await db.AdImpressionsDaily.SingleOrDefaultAsync(
                candidate => candidate.CreativeId == creativeId && candidate.BranchId == request.BranchId && candidate.Day == day, ct);
            if (row is null)
            {
                row = new AdImpressionDailyEntity
                {
                    AdImpressionDailyId = Guid.NewGuid(), CreativeId = creativeId, OrganizationId = request.OrganizationId,
                    BranchId = request.BranchId, Day = day
                };
                db.AdImpressionsDaily.Add(row);
            }

            row.Impressions += impressions;
            row.ShownMs += shownMs;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Та же пачка пришла дважды одновременно: вторая упёрлась в ключ пачки.
            db.ChangeTracker.Clear();
            if (await db.AdImpressionBatches.AsNoTracking()
                    .AnyAsync(batch => batch.DeviceId == request.DeviceId && batch.BatchId == request.BatchId, ct))
            {
                return IngestOutcome.Duplicate;
            }

            throw;
        }

        return IngestOutcome.Accepted;
    }

    public static async Task<IReadOnlyList<AdImpressionRowDto>> ReportAsync(
        PlatformDbContext db, DateOnly firstDay, DateOnly lastDay, Guid? campaignId, CancellationToken ct)
    {
        var rows = await (
                from impression in db.AdImpressionsDaily.AsNoTracking()
                join creative in db.AdCreatives.AsNoTracking() on impression.CreativeId equals creative.CreativeId
                join campaign in db.AdCampaigns.AsNoTracking() on creative.CampaignId equals campaign.CampaignId
                join organization in db.Organizations.AsNoTracking() on impression.OrganizationId equals organization.OrganizationId into organizations
                from organization in organizations.DefaultIfEmpty()
                join branch in db.Branches.AsNoTracking() on impression.BranchId equals branch.BranchId into branches
                from branch in branches.DefaultIfEmpty()
                where impression.Day >= firstDay && impression.Day <= lastDay && (campaignId == null || campaign.CampaignId == campaignId)
                orderby impression.Day descending, campaign.Name, creative.Title
                select new
                {
                    impression.Day, campaign.CampaignId, CampaignName = campaign.Name, creative.CreativeId, creative.Title,
                    impression.OrganizationId, OrganizationName = organization == null ? string.Empty : organization.Name,
                    impression.BranchId, BranchName = branch == null ? string.Empty : branch.Name,
                    City = branch == null ? string.Empty : branch.City, impression.Impressions, impression.ShownMs
                })
            .ToListAsync(ct);
        return rows.Select(row => new AdImpressionRowDto(
                row.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.CampaignId, row.CampaignName, row.CreativeId, row.Title,
                row.OrganizationId, row.OrganizationName, row.BranchId, row.BranchName, row.City, row.Impressions, row.ShownMs / 1000))
            .ToList();
    }
}
