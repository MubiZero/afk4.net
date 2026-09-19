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

    /// Журнал платформы смотрят поверх всей сети, и опознать клуб по идентификатору нельзя —
    /// наизусть их не знает никто, а строк на экране сотня.
    [Fact]
    public async Task Search_NamesTheClubTheRecordBelongsTo()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = Guid.NewGuid();
        await SeedOrganizationAsync(factory, organizationId, "Клуб на Рудаки");
        await SeedAsync(factory, organizationId, "organizations.status.update");

        var response = await client.GetAsync($"/api/platform/audit?organizationId={organizationId:D}");
        var body = await response.Content.ReadFromJsonAsync<AuditSearchResultDto>();

        var record = Assert.Single(body!.Records);
        Assert.Equal("Клуб на Рудаки", record.OrganizationName);
    }

    /// Организацию могли удалить, а её записи в журнале остаются: имени нет, но запись должна
    /// читаться и без него.
    [Fact]
    public async Task Search_WithoutTheOrganization_StillReturnsTheRecord()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = Guid.NewGuid();
        await SeedAsync(factory, organizationId, "organizations.status.update");

        var response = await client.GetAsync($"/api/platform/audit?organizationId={organizationId:D}");
        var body = await response.Content.ReadFromJsonAsync<AuditSearchResultDto>();

        var record = Assert.Single(body!.Records);
        Assert.Null(record.OrganizationName);
    }

    private static async Task SeedOrganizationAsync(PlatformApiFactory factory, Guid organizationId, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = $"club-{organizationId:N}"[..12],
            Name = name,
            Status = "active",
            PlanCode = "starter",
            SubscriptionStatus = "trial",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
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
