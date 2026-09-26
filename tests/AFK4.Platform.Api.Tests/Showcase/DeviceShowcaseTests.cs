using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Showcase;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Media;
using AFK4.Shared.Contracts.News;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Showcase;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Tournaments;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Showcase;

/// <summary>Витрина свободного ПК (P7a): что попадает на экран, в каком порядке и как агент узнаёт, что ничего не изменилось.</summary>
public sealed class DeviceShowcaseTests
{
    [Fact]
    public async Task TheShowcase_CarriesMarkedNewsFeaturedItemsAndAutomaticCards_InOrder()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var seeded = await SeedClubAsync(fixture);

        var showcase = (await (await GetAsync(fixture)).Content.ReadFromJsonAsync<DeviceShowcaseDto>())!;

        Assert.Equal(
            [
                ShowcaseCardKindNames.News, ShowcaseCardKindNames.Tariff, ShowcaseCardKindNames.Product,
                ShowcaseCardKindNames.Tournament, ShowcaseCardKindNames.Packages, ShowcaseCardKindNames.BarHit
            ],
            showcase.Cards.Select(card => card.Kind));

        var news = showcase.Cards[0];
        Assert.Equal("Ночь CS2 в пятницу", news.Title);
        Assert.Equal("https://media.example/news.webp", news.ImageUrl);

        var tariff = showcase.Cards[1];
        Assert.Equal("Ночной", tariff.Title);
        Assert.Equal(new MoneyDto("TJS", 600), tariff.Price);
        Assert.Equal("22:00–06:00", tariff.TimeWindow);

        var product = showcase.Cards[2];
        Assert.Equal("Бургер", product.Title);
        Assert.Equal("https://media.example/burger.webp", product.ImageUrl);

        var tournament = showcase.Cards[3];
        Assert.Equal("Кубок зала", tournament.Title);
        Assert.Equal("Dota 2", tournament.Subtitle);
        Assert.Null(tournament.Price);
        Assert.Equal(DevicePlayerFixture.Start.AddDays(3), tournament.StartsAtUtc);

        var packages = showcase.Cards[4];
        Assert.Equal(["3 часа", "5 часов"], packages.Packages!.Select(line => line.Name));
        Assert.Equal(180, packages.Packages![0].Minutes);

        var hit = showcase.Cards[5];
        Assert.Equal($"bar_hit:{seeded.HitProductId:N}", hit.CardId);
        Assert.Equal("Кола", hit.Title);
    }

    [Fact]
    public async Task AnUnchangedShowcase_AnswersNotModified_ToTheSameETag()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        var first = await GetAsync(fixture);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        var again = await GetAsync(fixture, etag);
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
    }

    [Fact]
    public async Task TheShowcase_WithoutTheDeviceKey_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(fixture, secret: "не-тот-ключ")).StatusCode);
    }

    // Отметки ставит Панель: новость, тариф и товар отвечают тем, что сохранили.
    [Fact]
    public async Task ThePanel_MarksNewsTariffsAndProducts_ForThePcScreen()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var seeded = await SeedClubAsync(fixture);
        var organizationRoute = $"/api/organizations/{fixture.Device.OrganizationId:D}";
        var branchRoute = $"{organizationRoute}/branches/{fixture.Device.BranchId:D}";

        var created = await fixture.Client.PostAsJsonAsync($"{organizationRoute}/news", new CreateNewsItemRequest(
            fixture.Device.BranchId, "Скидка на ночь", "С 22:00", null, IsPublished: true, null, null, ShowOnPcs: true));
        Assert.True((await created.Content.ReadFromJsonAsync<NewsItemDto>())!.ShowOnPcs);

        var tariff = await fixture.Client.PatchAsJsonAsync($"{branchRoute}/tariffs/{seeded.PlainTariffId:D}",
            new UpdateTariffRequest(fixture.Device.OrganizationId, "Дневной", IsActive: true, FeaturedOnPcs: true));
        Assert.True((await tariff.Content.ReadFromJsonAsync<TariffDto>())!.FeaturedOnPcs);

        var product = await fixture.Client.PatchAsJsonAsync($"{branchRoute}/pos/products/{seeded.HitProductId:D}",
            ProductUpdate(fixture, seeded.CategoryId, "https://media.example/cola.webp"));
        var saved = (await product.Content.ReadFromJsonAsync<PosProductDto>())!;
        Assert.True(saved.FeaturedOnPcs);
        Assert.Equal("https://media.example/cola.webp", saved.ImageUrl);

        var badImage = await fixture.Client.PatchAsJsonAsync($"{branchRoute}/pos/products/{seeded.HitProductId:D}",
            ProductUpdate(fixture, seeded.CategoryId, "file:///C:/cola.png"));
        Assert.Equal(HttpStatusCode.BadRequest, badImage.StatusCode);
    }

    [Fact]
    public async Task AnUpload_WithAnUnknownPurpose_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        using var form = new MultipartFormDataContent
        {
            { new StringContent("anything"), "purpose" },
            { new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0]), "file", "a.png" }
        };
        var response = await fixture.Client.PostAsync(
            MediaRoutes.BranchMedia(fixture.Device.OrganizationId, fixture.Device.BranchId), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void ALongBody_IsCutAtAWord_AndTheWindowReadsAsClockTime()
    {
        var body = string.Join(' ', Enumerable.Repeat("турнир", 80));

        var clipped = DeviceShowcase.Clip(body)!;

        Assert.True(clipped.Length <= ShowcaseLimits.MaxBodyLength + 1);
        Assert.EndsWith("турнир…", clipped);
        Assert.Null(DeviceShowcase.Clip("   "));
        Assert.Equal("09:30–18:00", DeviceShowcase.TimeWindow(570, 1080));
        Assert.Null(DeviceShowcase.TimeWindow(null, null));
    }

    private sealed record SeededClub(Guid PlainTariffId, Guid HitProductId, Guid CategoryId);

    private static UpdateProductRequest ProductUpdate(DevicePlayerFixture fixture, Guid categoryId, string imageUrl) =>
        new(fixture.Device.OrganizationId, categoryId, "Кола", "COLA", new MoneyDto("TJS", 1000),
            TrackStock: false, AllowNegativeStock: false, IsActive: true, FeaturedOnPcs: true, ImageUrl: imageUrl);

    private static async Task<SeededClub> SeedClubAsync(DevicePlayerFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = fixture.Device.OrganizationId;
        var branchId = fixture.Device.BranchId;
        var now = DevicePlayerFixture.Start;

        NewsItemEntity News(string title, bool showOnPcs, Guid? branch, DateTimeOffset created) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branch, Title = title, Body = "Приходи",
            ImageUrl = "https://media.example/news.webp", IsPublished = true, ShowOnPcs = showOnPcs,
            CreatedAtUtc = created, UpdatedAtUtc = created
        };
        db.NewsItems.AddRange(
            News("Ночь CS2 в пятницу", showOnPcs: true, branch: null, now.AddHours(-1)),
            News("Только для приложения", showOnPcs: false, branch: null, now),
            News("Другой зал", showOnPcs: true, branch: Guid.NewGuid(), now));

        var nightTariff = Guid.NewGuid();
        var plainTariff = Guid.NewGuid();
        db.Tariffs.AddRange(
            new TariffEntity
            {
                TariffId = nightTariff, OrganizationId = organizationId, BranchId = branchId, Name = "Ночной",
                IsActive = true, FeaturedOnPcs = true, AppliesFromMinuteOfDay = 22 * 60, AppliesToMinuteOfDay = 6 * 60,
                CreatedAtUtc = now
            },
            new TariffEntity
            {
                TariffId = plainTariff, OrganizationId = organizationId, BranchId = branchId, Name = "Дневной",
                IsActive = true, CreatedAtUtc = now
            });
        foreach (var tariffId in new[] { nightTariff, plainTariff })
        {
            db.TariffVersions.Add(new TariffVersionEntity
            {
                TariffVersionId = Guid.NewGuid(), TariffId = tariffId, OrganizationId = organizationId, BranchId = branchId,
                VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 10, MinimumBillableMinutes = 30,
                RoundingIncrementMinutes = 15, EffectiveFromUtc = now.AddDays(-1), CreatedAtUtc = now
            });
        }

        var categoryId = Guid.NewGuid();
        db.PosProductCategories.Add(new PosProductCategoryEntity
        {
            CategoryId = categoryId, OrganizationId = organizationId, BranchId = branchId, Name = "Бар", IsActive = true,
            CreatedAtUtc = now
        });
        PosProductEntity Product(string name, bool featured, string? image = null) => new()
        {
            ProductId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, CategoryId = categoryId,
            Name = name, Sku = name.ToUpperInvariant(), CurrencyCode = "TJS", PriceMinorUnits = 1000, IsActive = true,
            FeaturedOnPcs = featured, ImageUrl = image, CreatedAtUtc = now
        };
        var burger = Product("Бургер", featured: true, "https://media.example/burger.webp");
        var cola = Product("Кола", featured: false);
        var water = Product("Вода", featured: false);
        db.PosProducts.AddRange(burger, cola, water);

        // Кола — пять штук оплаченными чеками, вода — две (не хит), бургер продан больше всех, но
        // он уже выделен клубом и хитом не повторяется.
        void Sale(PosProductEntity product, int units, string state = PosSaleStateNames.Paid)
        {
            var saleId = Guid.NewGuid();
            db.PosSales.Add(new PosSaleEntity
            {
                PosSaleId = saleId, OrganizationId = organizationId, BranchId = branchId, ShiftId = Guid.NewGuid(),
                State = state, CurrencyCode = "TJS", TotalMinorUnits = units * 1000, CreatedAtUtc = now.AddDays(-2),
                PaidAtUtc = state == PosSaleStateNames.Paid ? now.AddDays(-2) : null
            });
            db.PosSaleLines.Add(new PosSaleLineEntity
            {
                PosSaleLineId = Guid.NewGuid(), PosSaleId = saleId, ProductId = product.ProductId, ProductName = product.Name,
                Quantity = units, CurrencyCode = "TJS", UnitPriceMinorUnits = 1000, LineTotalMinorUnits = units * 1000
            });
        }
        Sale(burger, 9);
        Sale(cola, 5);
        Sale(water, 2);
        Sale(water, 7, PosSaleStateNames.Voided);

        TournamentEntity Tournament(string title, DateTimeOffset startsAt) => new()
        {
            TournamentId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, Title = title,
            Discipline = "Dota 2", StartsAtUtc = startsAt, CurrencyCode = "TJS", State = TournamentStateNames.Published,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Tournaments.AddRange(Tournament("Кубок зала", now.AddDays(3)), Tournament("Далёкий кубок", now.AddDays(20)));

        PackageDefinitionEntity Package(string name, long price, int hours) => new()
        {
            PackageDefinitionId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, Name = name,
            CurrencyCode = "TJS", PriceMinorUnits = price, IncludedSeconds = hours * 3600, ExpiresAfterDays = 30,
            CreatedAtUtc = now
        };
        db.PackageDefinitions.AddRange(Package("5 часов", 4000, 5), Package("3 часа", 2500, 3));

        await db.SaveChangesAsync();
        return new SeededClub(plainTariff, cola.ProductId, categoryId);
    }

    private static Task<HttpResponseMessage> GetAsync(DevicePlayerFixture fixture, EntityTagHeaderValue? etag = null, string? secret = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Get,
            ShowcaseRoutes.Device(fixture.Device.DeviceId, fixture.Device.OrganizationId, fixture.Device.BranchId));
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secret ?? fixture.Device.CredentialSecret);
        if (etag is not null) message.Headers.IfNoneMatch.Add(etag);
        return fixture.Client.SendAsync(message);
    }
}
