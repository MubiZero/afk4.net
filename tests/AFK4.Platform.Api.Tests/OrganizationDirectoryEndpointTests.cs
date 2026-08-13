using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branding;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// The mobile customer app has no hostname to derive a club from — unlike the web build, where
/// the subdomain carries it. Sign-in needs an organization id, so the app has to let the player
/// pick one, and this catalogue is what it picks from.
public sealed class OrganizationDirectoryEndpointTests
{
    private static async Task SeedOrgAsync(
        PlatformApiFactory factory,
        string slug,
        string name,
        string status = "active")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            Status = status,
            LogoUrl = $"https://cdn.example/{slug}.png",
            AccentColor = "#2cc592",
            CreatedAtUtc = DateTimeOffset.Parse("2026-08-11T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-08-11T00:00:00Z")
        });
        await db.SaveChangesAsync();
    }

    /// A club with a hall, a price and a pin on the map — the state a filled-in club is in.
    private static async Task<Guid> SeedShowcaseAsync(
        PlatformApiFactory factory,
        string slug,
        long pricePerMinuteMinorUnits = 50,
        double? latitude = 38.5598,
        double? longitude = 68.7870,
        int seats = 3,
        DateTimeOffset? retiredAtUtc = null,
        DateTimeOffset? effectiveFromUtc = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = db.Organizations.Single(o => o.Slug == slug).OrganizationId;
        var branchId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var tariffId = Guid.NewGuid();
        var created = DateTimeOffset.Parse("2026-08-11T00:00:00Z");

        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = $"{slug}-main",
            Name = "На Рудаки",
            City = "Душанбе",
            Address = "пр. Рудаки, 1",
            Description = "40 машин и две PlayStation",
            CoverImageUrl = $"https://cdn.example/{slug}-hall.jpg",
            WorkingHoursJson = AFK4.Platform.Api.Branches.BranchWorkingHours.Serialize(
                Enumerable.Range(1, 7)
                    .Select(day => new AFK4.Shared.Contracts.Branches.BranchWorkingHoursDayDto(
                        day, day == 7, day == 7 ? null : "10:00", day == 7 ? null : "23:00"))
                    .ToList()),
            PhotosJson = AFK4.Platform.Api.Branches.BranchPhotos.Serialize(
            [
                new AFK4.Shared.Contracts.Branches.BranchPhotoDto($"https://cdn.example/{slug}-vip.jpg", null),
                new AFK4.Shared.Contracts.Branches.BranchPhotoDto($"https://cdn.example/{slug}-bar.jpg", null),
            ]),
            Latitude = latitude,
            Longitude = longitude,
            CreatedAtUtc = created
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = organizationId,
            BranchId = branchId,
            Name = "Основной зал",
            SortOrder = 1,
            CreatedAtUtc = created
        });
        for (var index = 0; index < seats; index++)
        {
            db.Seats.Add(new SeatEntity
            {
                SeatId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                ZoneId = zoneId,
                Name = $"PC-{index:D2}",
                SortOrder = index,
                CreatedAtUtc = created
            });
        }

        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId,
            OrganizationId = organizationId,
            BranchId = branchId,
            Name = "День",
            IsActive = true,
            CreatedAtUtc = created
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = Guid.NewGuid(),
            TariffId = tariffId,
            OrganizationId = organizationId,
            BranchId = branchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = pricePerMinuteMinorUnits,
            MinimumBillableMinutes = 60,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = effectiveFromUtc ?? created,
            RetiredAtUtc = retiredAtUtc,
            CreatedAtUtc = created
        });

        await db.SaveChangesAsync();
        return branchId;
    }

    [Fact]
    public async Task GetDirectory_ReturnsActiveOrganizations()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        await SeedOrgAsync(factory, "arena", "Arena Club");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrganizationDirectoryEntryDto[]>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Length);
        var cyberx = Assert.Single(body, entry => entry.Slug == "cyberx");
        Assert.Equal("CyberX", cyberx.Name);
        Assert.Equal("https://cdn.example/cyberx.png", cyberx.LogoUrl);
        Assert.NotEqual(Guid.Empty, cyberx.OrganizationId);
    }

    /// A suspended or offboarded club must not be pickable: the player would get through the
    /// picker only to be refused at sign-in, with nothing on screen explaining why.
    [Fact]
    public async Task GetDirectory_OmitsOrganizationsThatAreNotActive()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "live", "Live Club");
        await SeedOrgAsync(factory, "frozen", "Frozen Club", status: "suspended");
        await SeedOrgAsync(factory, "gone", "Gone Club", status: "offboarded");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        Assert.NotNull(body);
        Assert.Equal("live", Assert.Single(body!).Slug);
    }

    [Fact]
    public async Task GetDirectory_FiltersByNameCaseInsensitively()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        await SeedOrgAsync(factory, "arena", "Arena Club");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>(
            "/api/public/organizations?query=arena");

        Assert.NotNull(body);
        Assert.Equal("arena", Assert.Single(body!).Slug);
    }

    [Fact]
    public async Task GetDirectory_FiltersBySlugToo()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "Клуб на Рудаки");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>(
            "/api/public/organizations?query=cyber");

        Assert.NotNull(body);
        Assert.Equal("cyberx", Assert.Single(body!).Slug);
    }

    [Fact]
    public async Task GetDirectory_SortsByNameSoTheListIsStableBetweenCalls()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "zeta", "Zeta");
        await SeedOrgAsync(factory, "alpha", "Alpha");
        await SeedOrgAsync(factory, "mid", "Mid");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        Assert.NotNull(body);
        Assert.Equal(new[] { "Alpha", "Mid", "Zeta" }, body!.Select(entry => entry.Name).ToArray());
    }

    /// The catalogue is a shop window, nothing more. Anything about how the business is doing —
    /// subscription state, debt, how many players or branches it has — is not the public's business
    /// and must never leak through here.
    [Fact]
    public async Task GetDirectory_ExposesOnlyShopWindowFields()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        using var client = factory.CreateClient();

        var raw = await client.GetStringAsync("/api/public/organizations");

        foreach (var forbidden in new[]
                 {
                     "status", "subscription", "plan", "debt", "billing",
                     "createdAt", "updatedAt", "limits", "features"
                 })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// An unbounded catalogue is a free bulk export of every club on the platform. Cap it, and
    /// make the client narrow the search instead of scrolling the whole tenant table.
    [Fact]
    public async Task GetDirectory_CapsResultCount()
    {
        await using var factory = new PlatformApiFactory();
        for (var index = 0; index < 60; index++)
        {
            await SeedOrgAsync(factory, $"club{index:D2}", $"Club {index:D2}");
        }

        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        Assert.NotNull(body);
        Assert.Equal(50, body!.Length);
    }

    /// Клуб выбирают по тому, где он и сколько стоит час. Одно название не отвечает ни на один
    /// из этих вопросов, поэтому витрина несёт адрес, фото зала, цену и количество мест.
    [Fact]
    public async Task GetDirectory_ShowsWhereTheClubIsAndWhatAnHourCosts()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        await SeedShowcaseAsync(factory, "cyberx", pricePerMinuteMinorUnits: 50, seats: 40);
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var club = Assert.Single(body!);
        Assert.Equal(3000, club.PricePerHourFromMinorUnits);
        Assert.Equal("TJS", club.CurrencyCode);
        Assert.Equal(40, club.SeatCount);
        var place = Assert.Single(club.Places!);
        Assert.Equal("Душанбе", place.City);
        Assert.Equal("пр. Рудаки, 1", place.Address);
        Assert.Equal("https://cdn.example/cyberx-hall.jpg", place.CoverImageUrl);
        Assert.Equal(38.5598, place.Latitude);
        Assert.Equal(68.7870, place.Longitude);
    }

    /// «Открыт ли он сейчас» — вопрос, который игрок задаёт до выхода из дома, а ответ на
    /// него уже лежал в базе и никуда не отдавался.
    [Fact]
    public async Task GetDirectory_CarriesTheOpeningHours()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        await SeedShowcaseAsync(factory, "cyberx");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var place = Assert.Single(Assert.Single(body!).Places!);
        Assert.Equal(7, place.WorkingHours!.Count);
        Assert.Equal("23:00", place.WorkingHours.Single(day => day.DayOfWeek == 1).CloseTime);
        Assert.True(place.WorkingHours.Single(day => day.DayOfWeek == 7).IsClosed);
    }

    /// Одно фото отвечает «как тут выглядит», галерея — «а что ещё». Обложка идёт первой:
    /// её владелец выбрал лицом зала.
    [Fact]
    public async Task GetDirectory_CarriesTheHallPhotosCoverFirst()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        await SeedShowcaseAsync(factory, "cyberx");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var place = Assert.Single(Assert.Single(body!).Places!);
        Assert.Equal(
            new[]
            {
                "https://cdn.example/cyberx-hall.jpg",
                "https://cdn.example/cyberx-vip.jpg",
                "https://cdn.example/cyberx-bar.jpg"
            },
            place.PhotoUrls!.ToArray());
    }

    /// Снятый с публикации тариф назвал бы игроку цену, которую на кассе никто не подтвердит.
    [Fact]
    public async Task GetDirectory_IgnoresRetiredAndFutureTariffs()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "retired", "Retired Price");
        await SeedShowcaseAsync(factory, "retired", pricePerMinuteMinorUnits: 10,
            retiredAtUtc: DateTimeOffset.Parse("2026-08-12T00:00:00Z"));
        await SeedOrgAsync(factory, "future", "Future Price");
        await SeedShowcaseAsync(factory, "future", pricePerMinuteMinorUnits: 10,
            effectiveFromUtc: DateTimeOffset.UtcNow.AddDays(30));
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        Assert.All(body!, club => Assert.Null(club.PricePerHourFromMinorUnits));
    }

    /// Незаполненный клуб остаётся в списке: пустая витрина — повод её заполнить, а не причина
    /// пропасть из каталога, в котором игрок его ищет.
    [Fact]
    public async Task GetDirectory_KeepsClubsThatFilledNothingIn()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "bare", "Bare Club");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var club = Assert.Single(body!);
        Assert.Empty(club.Places!);
        Assert.Null(club.PricePerHourFromMinorUnits);
        Assert.Equal(0, club.SeatCount);
    }

    /// Витрина одного клуба не должна считать чужие места и чужие цены.
    [Fact]
    public async Task GetDirectory_KeepsShowcasesOfDifferentClubsApart()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "alpha", "Alpha");
        await SeedShowcaseAsync(factory, "alpha", pricePerMinuteMinorUnits: 50, seats: 10);
        await SeedOrgAsync(factory, "beta", "Beta");
        await SeedShowcaseAsync(factory, "beta", pricePerMinuteMinorUnits: 80, seats: 4);
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var alpha = Assert.Single(body!, club => club.Slug == "alpha");
        var beta = Assert.Single(body!, club => club.Slug == "beta");
        Assert.Equal(3000, alpha.PricePerHourFromMinorUnits);
        Assert.Equal(10, alpha.SeatCount);
        Assert.Equal(4800, beta.PricePerHourFromMinorUnits);
        Assert.Equal(4, beta.SeatCount);
    }

    [Fact]
    public async Task GetDirectory_BlankQueryBehavesLikeNoQuery()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx", "CyberX");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>(
            "/api/public/organizations?query=%20%20");

        Assert.NotNull(body);
        Assert.Single(body!);
    }
}
