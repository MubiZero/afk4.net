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
