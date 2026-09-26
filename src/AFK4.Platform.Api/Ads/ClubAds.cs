using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Showcase;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Ads;

/// <summary>
/// Что из рекламы платформы идёт и шло на ПК клуба (спека рекламы, §8.4). Считается так же, как
/// витрина ПК, — теми же правилами подбора, — плюс показы на ПК клуба за последние тридцать дней.
/// </summary>
public static class ClubAds
{
    public static async Task<ClubAdsDto> ListAsync(
        PlatformDbContext db, Guid organizationId, bool adsEnabled, DateTimeOffset now, string apiBaseUrl, CancellationToken ct)
    {
        var to = DateOnly.FromDateTime(now.UtcDateTime);
        var from = to.AddDays(-(ClubAdsLimits.WindowDays - 1));

        var shown = await db.AdImpressionsDaily.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.Day >= from && row.Day <= to)
            .GroupBy(row => row.CreativeId)
            .Select(group => new { CreativeId = group.Key, Impressions = group.Sum(row => row.Impressions), ShownMs = group.Sum(row => row.ShownMs), LastDay = group.Max(row => row.Day) })
            .ToDictionaryAsync(row => row.CreativeId, ct);

        // Идёт сейчас — то, что витрина положила бы хоть одному залу клуба. Без рекламы на тарифе — ничего.
        var running = new HashSet<Guid>();
        if (adsEnabled)
        {
            var cities = await db.Branches.AsNoTracking()
                .Where(branch => branch.OrganizationId == organizationId)
                .Select(branch => branch.City)
                .Distinct()
                .ToListAsync(ct);
            foreach (var city in cities)
            {
                var (_, creatives) = await PlatformAds.EligibleAsync(db, organizationId, city ?? string.Empty, now, ct);
                running.UnionWith(creatives.Select(creative => creative.CreativeId));
            }
        }

        var ids = shown.Keys.Union(running).ToList();
        var creativesById = await db.AdCreatives.AsNoTracking().Where(creative => ids.Contains(creative.CreativeId)).ToDictionaryAsync(creative => creative.CreativeId, ct);
        var campaignIds = creativesById.Values.Select(creative => creative.CampaignId).Distinct().ToList();
        var campaigns = await db.AdCampaigns.AsNoTracking().Where(campaign => campaignIds.Contains(campaign.CampaignId)).ToDictionaryAsync(campaign => campaign.CampaignId, ct);
        var advertiserIds = campaigns.Values.Select(campaign => campaign.AdvertiserId).Distinct().ToList();
        var advertisers = await db.AdAdvertisers.AsNoTracking().Where(advertiser => advertiserIds.Contains(advertiser.AdvertiserId)).ToDictionaryAsync(advertiser => advertiser.AdvertiserId, ct);
        var images = await db.AdCreativeImages.AsNoTracking()
            .Where(image => ids.Contains(image.CreativeId))
            .Select(image => new { image.CreativeId, image.ContentType, image.Sha256 })
            .ToDictionaryAsync(image => image.CreativeId, ct);

        var ads = creativesById.Values
            .Where(creative => campaigns.ContainsKey(creative.CampaignId))
            .Select(creative =>
            {
                var campaign = campaigns[creative.CampaignId];
                advertisers.TryGetValue(campaign.AdvertiserId, out var advertiser);
                shown.TryGetValue(creative.CreativeId, out var counted);
                return new ClubAdDto(
                    creative.CreativeId,
                    advertiser?.Name ?? string.Empty,
                    campaign.Category,
                    creative.Title,
                    creative.Body,
                    creative.TitleRu,
                    creative.BodyRu,
                    images.TryGetValue(creative.CreativeId, out var image) ? PlatformAds.ImageUrl(apiBaseUrl, creative.CreativeId, image.ContentType, image.Sha256) : null,
                    campaign.StartsAtUtc,
                    campaign.EndsAtUtc,
                    running.Contains(creative.CreativeId),
                    counted?.Impressions ?? 0,
                    (counted?.ShownMs ?? 0) / 1000,
                    counted?.LastDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    campaign.DistanceSelling && advertiser is not null ? new ShowcaseSellerDto(advertiser.LegalName, advertiser.TaxId, advertiser.Address) : null,
                    campaign.RequiresCertification,
                    campaign.ContainsOffer ? campaign.EndsAtUtc : null);
            })
            .OrderByDescending(ad => ad.Running)
            .ThenByDescending(ad => ad.LastShownDay)
            .ThenBy(ad => ad.Advertiser, StringComparer.CurrentCulture)
            .ToList();

        return new ClubAdsDto(adsEnabled, from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ads);
    }
}
