using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OrganizationBrandingEndpointTests
{
    private static async Task<Guid> SeedOrgAsync(PlatformApiFactory factory, string slug, string status = "active")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var id = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = id,
            Slug = slug,
            Name = "CyberX",
            Status = status,
            LogoUrl = "https://cdn.example/cyberx.png",
            AccentColor = "#c8ff00",
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task SeedBranchAsync(
        PlatformApiFactory factory, Guid organizationId, string name, string city, string address)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Branches.Add(new BranchEntity
        {
            BranchId = Guid.NewGuid(),
            OrganizationId = organizationId,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Name = name,
            City = city,
            Address = address,
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetBranding_KnownActiveSlug_ReturnsBranding()
    {
        await using var factory = new PlatformApiFactory();
        var orgId = await SeedOrgAsync(factory, "cyberx");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/organization/cyberx/branding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrganizationBrandingDto>();
        Assert.NotNull(body);
        Assert.Equal(orgId, body!.OrganizationId);
        Assert.Equal("CyberX", body.Name);
        Assert.Equal("#c8ff00", body.AccentColor);
        Assert.Equal("https://cdn.example/cyberx.png", body.LogoUrl);
    }

    // Веб-сборка клуба узнаёт его залы отсюда: без них сеть из нескольких залов оказалась бы
    // тупиком — счёт человеку открывает первое действие, а зал за него сервер не гадает.
    [Fact]
    public async Task GetBranding_ReturnsTheHallsOfTheNetwork()
    {
        await using var factory = new PlatformApiFactory();
        var orgId = await SeedOrgAsync(factory, "cyberx");
        await SeedBranchAsync(factory, orgId, "На Рудаки", "Душанбе", "пр. Рудаки, 1");
        await SeedBranchAsync(factory, orgId, "В Худжанде", "Худжанд", "ул. Ленина, 5");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationBrandingDto>(
            "/api/public/organization/cyberx/branding");

        Assert.NotNull(body!.Halls);
        // Порядок — по городу, затем по названию: список должен выглядеть одинаково при каждом
        // открытии, иначе выбранный глазом зал уезжает под пальцем.
        Assert.Equal(["На Рудаки", "В Худжанде"], body.Halls!.Select(h => h.Name));
        Assert.Equal("пр. Рудаки, 1", body.Halls[0].Address);
        Assert.Equal("Худжанд", body.Halls[1].City);
    }

    // Клуб без залов — не ошибка: так выглядит только что заведённая организация, и веб должен
    // работать как раньше, а не показывать пустой вопрос «в какой зал вы придёте».
    [Fact]
    public async Task GetBranding_OrgWithoutBranches_AnswersWithAnEmptyHallList()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "cyberx");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationBrandingDto>(
            "/api/public/organization/cyberx/branding");

        Assert.Empty(body!.Halls!);
    }

    // Чужие залы в ответ не попадают: соседний клуб на том же сервере — не часть этой сети.
    [Fact]
    public async Task GetBranding_LeavesOutTheHallsOfOtherOrganizations()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedOrgAsync(factory, "cyberx");
        var other = await SeedOrgAsync(factory, "arena");
        await SeedBranchAsync(factory, mine, "На Рудаки", "Душанбе", "пр. Рудаки, 1");
        await SeedBranchAsync(factory, other, "Чужой", "Душанбе", "ул. Сомони, 9");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<OrganizationBrandingDto>(
            "/api/public/organization/cyberx/branding");

        Assert.Equal(["На Рудаки"], body!.Halls!.Select(h => h.Name));
    }

    [Fact]
    public async Task GetBranding_MixedCaseSlug_IsNormalizedAndResolves()
    {
        await using var factory = new PlatformApiFactory();
        var orgId = await SeedOrgAsync(factory, "cyberx");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/organization/CyberX/branding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrganizationBrandingDto>();
        Assert.NotNull(body);
        Assert.Equal(orgId, body!.OrganizationId);
    }

    [Fact]
    public async Task GetBranding_UnknownSlug_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/organization/nope/branding");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBranding_SuspendedOrg_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "frozen", status: "suspended");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/organization/frozen/branding");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
