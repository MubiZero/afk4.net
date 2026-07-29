using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAuditEndpointTests
{
    [Fact]
    public async Task Search_WithPermission_AppliesOrganizationAndActionFilters()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = Guid.NewGuid();
        await SeedAsync(factory, organizationId, "updates.rollout.create");
        await SeedAsync(factory, Guid.NewGuid(), "updates.rollout.create");
        await SeedAsync(factory, organizationId, "other.action");

        var response = await client.GetAsync($"/api/platform/audit?organizationId={organizationId:D}&action=updates.rollout.create");
        var body = await response.Content.ReadFromJsonAsync<AuditSearchResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var record = Assert.Single(body.Records);
        Assert.Equal(organizationId, record.OrganizationId);
        Assert.Equal("updates.rollout.create", record.Action);
    }

    [Fact]
    public async Task Search_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/platform/audit")).StatusCode);
    }

    [Fact]
    public async Task Search_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/platform/audit")).StatusCode);
    }

    private static async Task SeedAsync(PlatformApiFactory factory, Guid organizationId, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(), OrganizationId = organizationId, Action = action,
            TargetType = "UpdateRollout", Outcome = AuditOutcome.Succeeded, SourceApp = "PlatformApi",
            DetailsJson = "{}", CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
