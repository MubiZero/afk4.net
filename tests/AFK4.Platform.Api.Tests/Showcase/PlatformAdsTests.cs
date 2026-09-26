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

    // Картинка, которую сервер скачивает при одобрении: подставной ответ вместо интернета.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static Action<IServiceCollection> Images(HttpStatusCode status = HttpStatusCode.OK) => services =>
        services.AddHttpClient(AdCreativeImages.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new ImageHandler(status));

    private sealed class ImageHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(Png) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }
    }

    private static UpsertAdvertiserRequest Advertiser() =>
        new("Сомон Телеком", "+992 00 000 00 00", "ООО «Сомон Телеком»", "123456789", "Душанбе, пр. Рудаки 1");

    [Fact]
    public async Task AnApprovedCampaign_ReachesAFreePlanClub_AndItsImpressionsAreCountedOnce()
    {
        await using var fixture = DevicePlayerFixture.Create(Images());
        await fixture.SeedAsync();
        using var platform = fixture.Factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(fixture.Factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin], clock: fixture.Clock);
        await PrepareClubAsync(fixture, city: "Душанбе", ads: true);

        var advertiser = await ReadAsync<AdvertiserDto>(await platform.PostAsJsonAsync(AdRoutes.Advertisers, Advertiser()));
        var endsAt = DevicePlayerFixture.Start.AddDays(30);
        var campaign = await ReadAsync<AdCampaignDto>(await platform.PostAsJsonAsync(AdRoutes.Campaigns, new UpsertAdCampaignRequest(
            advertiser.AdvertiserId, "Осень", AdCategoryNames.Telecom, DevicePlayerFixture.Start.AddDays(-1), endsAt,
            ["душанбе"], null, new AdCampaignComplianceDto(DistanceSelling: true, RequiresCertification: true, ContainsOffer: true))));
        var creative = await ReadAsync<AdCreativeDto>(await platform.PostAsJsonAsync($"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/creatives",
            new UpsertAdCreativeRequest("Бемаҳдуд барои як моҳ", "Барои бозигарон", "https://media.example/ad.png", "Безлимит на месяц", "Для геймеров")));
        var state = $"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/state";
        var moderation = $"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/creatives/{creative.CreativeId:D}/moderation";

        // Без одобренного креатива кампанию не запустить; одобрить можно, только отметив каждую строку закона.
        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsJsonAsync(state, new SetAdCampaignStateRequest(AdCampaignStateNames.Active))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsJsonAsync(moderation,
            new ModerateAdCreativeRequest(true, null, [AdModerationCheckNames.NotClubOrBetting, AdModerationCheckNames.NoBannedGoods]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsJsonAsync(moderation,
            new ModerateAdCreativeRequest(true, null, AdModerationCheckNames.All))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsJsonAsync(state, new SetAdCampaignStateRequest(AdCampaignStateNames.Active))).StatusCode);

        var showcase = (await ShowcaseAsync(fixture))!;
        var ad = Assert.Single(showcase.Cards, card => card.Kind == ShowcaseCardKindNames.Ad);
        Assert.Equal("Сомон Телеком", ad.Advertiser);
        // Таджикский — первым, русский — второй строкой; что велит закон — на самой карточке.
        Assert.Equal("Бемаҳдуд барои як моҳ", ad.Title);
        Assert.Equal("Безлимит на месяц", ad.SecondaryTitle);
        Assert.Equal(new ShowcaseSellerDto("ООО «Сомон Телеком»", "123456789", "Душанбе, пр. Рудаки 1"), ad.Seller);
        Assert.True(ad.RequiresCertification);
        Assert.Equal(endsAt, ad.OfferUntilUtc);

        // ПК видит копию, которую хранит сервер, а не адрес рекламодателя.
        Assert.StartsWith("http://localhost:5074" + AdRoutes.CreativeImage(creative.CreativeId), ad.ImageUrl);
        var image = await fixture.Client.GetAsync(AdRoutes.CreativeImage(creative.CreativeId));
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());

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

    // Показанную рекламу закон велит хранить год (ст. 22): одобренный креатив не правится, а снимается.
    [Fact]
    public async Task AnApprovedCreative_IsLocked_AndArchivingTakesItOffTheScreen()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await PrepareClubAsync(fixture, city: "Душанбе", ads: true);
        using var platform = fixture.Factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(fixture.Factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin], clock: fixture.Clock);
        var (campaignId, creativeId) = await SeedActiveCampaignAsync(fixture, cities: []);
        var creativePath = $"{AdRoutes.Campaigns}/{campaignId:D}/creatives/{creativeId:D}";

        var edit = await platform.PutAsJsonAsync(creativePath, new UpsertAdCreativeRequest("Матни нав", null, null));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Contains(AdErrorCodeNames.CreativeLocked, await edit.Content.ReadAsStringAsync());

        var archived = await ReadAsync<AdCreativeDto>(await platform.PostAsync($"{creativePath}/archive", null));
        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.DoesNotContain((await ShowcaseAsync(fixture))!.Cards, card => card.Kind == ShowcaseCardKindNames.Ad);
    }

    [Fact]
    public async Task AnImageThatCannotBeDownloaded_BlocksApproval()
    {
        await using var fixture = DevicePlayerFixture.Create(Images(HttpStatusCode.NotFound));
        await fixture.SeedAsync();
        using var platform = fixture.Factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(fixture.Factory, platform, roles: [PlatformAdminRoleNames.PlatformAdmin], clock: fixture.Clock);
        var advertiser = await ReadAsync<AdvertiserDto>(await platform.PostAsJsonAsync(AdRoutes.Advertisers, Advertiser()));
        var campaign = await ReadAsync<AdCampaignDto>(await platform.PostAsJsonAsync(AdRoutes.Campaigns, new UpsertAdCampaignRequest(
            advertiser.AdvertiserId, "Осень", AdCategoryNames.Telecom, DevicePlayerFixture.Start, DevicePlayerFixture.Start.AddDays(3), null, null)));
        var creative = await ReadAsync<AdCreativeDto>(await platform.PostAsJsonAsync($"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/creatives",
            new UpsertAdCreativeRequest("Бемаҳдуд", null, "https://media.example/gone.png")));

        var approve = await platform.PostAsJsonAsync($"{AdRoutes.Campaigns}/{campaign.CampaignId:D}/creatives/{creative.CreativeId:D}/moderation",
            new ModerateAdCreativeRequest(true, null, AdModerationCheckNames.All));

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Contains(AdErrorCodeNames.ImageUnavailable, await approve.Content.ReadAsStringAsync());
    }

    [Fact]
    public void TheLaw_AsCodeChecksIt()
    {
        // Реквизиты рекламодателя обязательны, ИНН — цифры.
        Assert.NotNull(PlatformAds.Validate(new UpsertAdvertiserRequest("Сомон", null)));
        Assert.NotNull(PlatformAds.Validate(Advertiser() with { TaxId = "12-34" }));
        Assert.Null(PlatformAds.Validate(Advertiser()));
        // «Здоровье и красота» — только с разрешением Минздрава.
        var cosmetics = new UpsertAdCampaignRequest(Guid.NewGuid(), "Крем", AdCategoryNames.HealthBeauty, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null, null);
        Assert.True(PlatformAds.NeedsPermit(cosmetics));
        Assert.False(PlatformAds.NeedsPermit(cosmetics with { Compliance = new AdCampaignComplianceDto(PermitNumber: "МЗ-0042") }));
        // Без таджикского заголовка креатива нет; превосходные степени модератор видит подсвеченными.
        Assert.NotNull(PlatformAds.Validate(new UpsertAdCreativeRequest(" ", null, null, "Лучший интернет")));
        Assert.Equal(["лучш", "беҳтарин"], PlatformAds.WordingFlags("Беҳтарин интернет", null, "Лучший интернет"));
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
