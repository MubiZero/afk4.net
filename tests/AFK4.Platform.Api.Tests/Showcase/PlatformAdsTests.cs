using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Ads;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Showcase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Showcase;

/// <summary>Реклама платформы в витрине ПК (P7b): модерация, показ у бесплатного тарифа, счёт показов.</summary>
public sealed class PlatformAdsTests
{
    private static ShowcaseCardDto Club(int index) => new($"news:{index}", ShowcaseCardKindNames.News, $"Новость {index}");

    private static ShowcaseCardDto Ad(int index) => new($"ad:{index}", ShowcaseCardKindNames.Ad, $"Реклама {index}", Advertiser: "Сомон");

    [Fact]
    public void AnAd_TakesEveryThirdPlace()
    {
        var mixed = PlatformAds.Interleave([Club(1), Club(2), Club(3), Club(4), Club(5)], [Ad(1), Ad(2), Ad(3)]);

        Assert.Equal(["news:1", "news:2", "ad:1", "news:3", "news:4", "ad:2", "news:5"], mixed.Select(card => card.CardId));
        Assert.Equal(["news:1", "ad:1"], PlatformAds.Interleave([Club(1)], [Ad(1), Ad(2)]).Select(card => card.CardId));
        Assert.Equal(3, PlatformAds.Interleave([], [Ad(1), Ad(2), Ad(3), Ad(4)]).Count);
        Assert.Equal(["news:1"], PlatformAds.Interleave([Club(1)], []).Select(card => card.CardId));
    }

    [Fact]
    public async Task AnApprovedCampaign_ReachesAFreePlanClub_AndItsImpressionsAreCountedOnce()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var platform = fixture.Factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(fixture.Factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin], clock: fixture.Clock);
        await PrepareClubAsync(fixture, city: "Душанбе", ads: true);

        var advertiser = await ReadAsync<AdvertiserDto>(await platform.PostAsJsonAsync(AdRoutes.Advertisers, new UpsertAdvertiserRequest("Сомон Телеком", "+992 00 000 00 00")));
        var campaign = await ReadAsync<AdCampaignDto>(await platform.PostAsJsonAsync(AdRoutes.Campaigns, new UpsertAdCampaignRequest(
            advertiser.AdvertiserId, "Осень", AdCategoryNames.Telecom, DevicePlayerFixture.Start.AddDays(-1), DevicePlayerFixture.Start.AddDays(30),
            ["душанбе"], null)));
        var creative = await ReadAsync<AdCreativeDto>(await platform.PostAsJsonAsync($"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/creatives",
            new UpsertAdCreativeRequest("Безлимит на месяц", "Для геймеров", "https://media.example/ad.webp")));
        var state = $"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/state";
        var moderation = $"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/creatives/{creative.CreativeId:D}/moderation";

        // Без одобренного креатива кампанию не запустить; одобрить без подтверждения модератора нельзя.
        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsJsonAsync(state, new SetAdCampaignStateRequest(AdCampaignStateNames.Active))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsJsonAsync(moderation, new ModerateAdCreativeRequest(true, null, ConfirmedAllowed: false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsJsonAsync(moderation, new ModerateAdCreativeRequest(true, null, ConfirmedAllowed: true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsJsonAsync(state, new SetAdCampaignStateRequest(AdCampaignStateNames.Active))).StatusCode);

        var showcase = (await ShowcaseAsync(fixture))!;
        var ad = Assert.Single(showcase.Cards, card => card.Kind == ShowcaseCardKindNames.Ad);
        Assert.Equal("Сомон Телеком", ad.Advertiser);
        Assert.Equal("Безлимит на месяц", ad.Title);

        var batch = new DeviceShowcaseImpressionsRequest(fixture.Device.OrganizationId, fixture.Device.BranchId, fixture.Device.DeviceId, "batch-1",
        [
            new ShowcaseImpressionDto(ad.CardId, "2026-09-24", 3, 27_000),
            new ShowcaseImpressionDto("news:1", "2026-09-24", 5, 45_000)
        ]);
        Assert.Equal(HttpStatusCode.NoContent, (await ImpressionsAsync(fixture, batch)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ImpressionsAsync(fixture, batch)).StatusCode);

        var report = (await platform.GetFromJsonAsync<AdImpressionRowDto[]>($"{AdRoutes.Report}?from=2026-09-01&to=2026-09-30"))!;
        var row = Assert.Single(report);
        Assert.Equal(3, row.Impressions);
        Assert.Equal(27, row.ShownSeconds);
        Assert.Equal("Душанбе", row.City);
    }

    [Fact]
    public async Task APaidClub_SeesNoAds_AndAnotherCitysCampaignStaysAway()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await PrepareClubAsync(fixture, city: "Худжанд", ads: false);
        await SeedActiveCampaignAsync(fixture, cities: ["Душанбе"]);

        var showcase = (await ShowcaseAsync(fixture))!;

        Assert.DoesNotContain(showcase.Cards, card => card.Kind == ShowcaseCardKindNames.Ad);
    }

    [Fact]
    public async Task EditingAnApprovedCreative_SendsItBackToModeration()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var platform = fixture.Factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(fixture.Factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin], clock: fixture.Clock);
        var (campaignId, creativeId) = await SeedActiveCampaignAsync(fixture, cities: []);

        var edited = await ReadAsync<AdCreativeDto>(await platform.PutAsJsonAsync($"{AdRoutes.Campaigns}/{campaignId:D}/creatives/{creativeId:D}",
            new UpsertAdCreativeRequest("Новый текст", null, null)));

        Assert.Equal(AdModerationNames.Pending, edited.Moderation);
    }

    [Fact]
    public async Task PlatformSupport_CannotManageAds_AndAnHttpImageIsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var support = fixture.Factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(fixture.Factory, support, roles: [PlatformAdminRoleNames.PlatformSupport], clock: fixture.Clock);
        Assert.Equal(HttpStatusCode.Forbidden, (await support.GetAsync(AdRoutes.Campaigns)).StatusCode);

        Assert.NotNull(PlatformAds.Validate(new UpsertAdCreativeRequest("Баннер", null, "http://media.example/a.png")));
        Assert.NotNull(PlatformAds.Validate(new UpsertAdCampaignRequest(Guid.NewGuid(), "Пиво", "alcohol", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null)));
    }

    private static async Task PrepareClubAsync(DevicePlayerFixture fixture, string city, bool ads)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await db.Branches.SingleAsync(candidate => candidate.BranchId == fixture.Device.BranchId);
        branch.City = city;
        if (ads)
        {
            db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
            {
                OrganizationFeatureOverrideId = Guid.NewGuid(), OrganizationId = fixture.Device.OrganizationId,
                FeatureKey = PlatformFeatureNames.PlatformAds, IsEnabled = true, Reason = "бесплатный тариф",
                SetAtUtc = DevicePlayerFixture.Start
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<(Guid CampaignId, Guid CreativeId)> SeedActiveCampaignAsync(DevicePlayerFixture fixture, IReadOnlyList<string> cities)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var advertiser = new AdAdvertiserEntity { AdvertiserId = Guid.NewGuid(), Name = "Сомон", CreatedAtUtc = DevicePlayerFixture.Start };
        var campaign = new AdCampaignEntity
        {
            CampaignId = Guid.NewGuid(), AdvertiserId = advertiser.AdvertiserId, Name = "Осень", Category = AdCategoryNames.Telecom,
            StartsAtUtc = DevicePlayerFixture.Start.AddDays(-1), EndsAtUtc = DevicePlayerFixture.Start.AddDays(1),
            CitiesJson = System.Text.Json.JsonSerializer.Serialize(cities), State = AdCampaignStateNames.Active,
            CreatedAtUtc = DevicePlayerFixture.Start, UpdatedAtUtc = DevicePlayerFixture.Start
        };
        var creative = new AdCreativeEntity
        {
            CreativeId = Guid.NewGuid(), CampaignId = campaign.CampaignId, Title = "Безлимит", Moderation = AdModerationNames.Approved,
            CreatedAtUtc = DevicePlayerFixture.Start
        };
        db.AddRange(advertiser, campaign, creative);
        await db.SaveChangesAsync();
        return (campaign.CampaignId, creative.CreativeId);
    }

    private static async Task<DeviceShowcaseDto?> ShowcaseAsync(DevicePlayerFixture fixture)
    {
        var message = new HttpRequestMessage(HttpMethod.Get,
            ShowcaseRoutes.Device(fixture.Device.DeviceId, fixture.Device.OrganizationId, fixture.Device.BranchId));
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, fixture.Device.CredentialSecret);
        var response = await fixture.Client.SendAsync(message);
        return await response.Content.ReadFromJsonAsync<DeviceShowcaseDto>();
    }

    private static Task<HttpResponseMessage> ImpressionsAsync(DevicePlayerFixture fixture, DeviceShowcaseImpressionsRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, AdRoutes.DeviceImpressions(fixture.Device.DeviceId)) { Content = JsonContent.Create(request) };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, fixture.Device.CredentialSecret);
        return fixture.Client.SendAsync(message);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
