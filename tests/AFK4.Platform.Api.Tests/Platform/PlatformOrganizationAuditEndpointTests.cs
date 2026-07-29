using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationAuditEndpointTests
{
    [Fact]
    public async Task GetAudit_WithPermission_ReturnsOnlyRequestedOrganizationHistory()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client);
        await SeedAuditAsync(factory, organizationId, "tenancy.organization.updated");
        await SeedAuditAsync(factory, Guid.NewGuid(), "other.organization.updated");

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId:D}/audit?limit=10");
        var body = await response.Content.ReadFromJsonAsync<AuditSearchResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body.Records, record => record.Action == "tenancy.organization.updated");
        Assert.DoesNotContain(body.Records, record => record.Action == "other.organization.updated");
    }

    [Fact]
    public async Task GetAudit_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_ForUnknownOrganization_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/audit");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/platform/organizations", new
        {
            organizationSlug = $"audit-{suffix}", organizationName = "Audit Organization",
            branchSlug = "main", branchName = "Main", branchCity = "Tashkent",
            planCode = "starter", subscriptionStatus = "trial",
            ownerUserName = $"owner-{suffix}@example.test", ownerDisplayName = "Owner"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AFK4.Shared.Contracts.Platform.Organizations.CreateOrganizationResponse>();
        return body!.Organization.OrganizationId;
    }

    private static async Task SeedAuditAsync(PlatformApiFactory factory, Guid organizationId, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(), OrganizationId = organizationId, Action = action,
            TargetType = "Organization", Outcome = AuditOutcome.Succeeded, SourceApp = "PlatformApi",
            DetailsJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
