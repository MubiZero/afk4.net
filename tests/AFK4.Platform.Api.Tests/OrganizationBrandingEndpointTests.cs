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
