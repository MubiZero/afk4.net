using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Health;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationHealthEndpointTests
{
    [Fact]
    public async Task GetHealth_WithValidOrganization_ReturnsCountsAndStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);
        await SeedDeviceAsync(factory, organizationId);
        await SeedStaffSignInAsync(factory, organizationId);

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId:D}/health");
        var body = await response.Content.ReadFromJsonAsync<OrganizationHealthDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(organizationId, body.OrganizationId);
        Assert.Equal(OrganizationStatusNames.Active, body.Status);
        Assert.Equal(1, body.BranchCount);
        Assert.Equal(1, body.DeviceCount);
        Assert.NotNull(body.LatestStaffSignInAtUtc);
        Assert.Equal(0, body.RecentErrorCount);
        Assert.Empty(body.RecentErrors);
    }

    [Fact]
    public async Task GetHealth_ListsRecentDeniedAuditAsErrors()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        await SeedAuditAsync(factory, organizationId, AuditOutcome.Denied, "tenancy.demo.fail", DateTimeOffset.UtcNow.AddHours(-1));
        await SeedAuditAsync(factory, organizationId, AuditOutcome.Succeeded, "tenancy.demo.ok", DateTimeOffset.UtcNow.AddHours(-2));

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId:D}/health");
        var body = await response.Content.ReadFromJsonAsync<OrganizationHealthDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(1, body.RecentErrorCount);
        Assert.Single(body.RecentErrors);
        Assert.Equal("tenancy.demo.fail", body.RecentErrors[0].Action);
        Assert.Equal(AuditOutcome.Denied, body.RecentErrors[0].Outcome);
    }

    [Fact]
    public async Task GetHealth_OmitsAuditOutsideRecentWindow()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);
        await SeedAuditAsync(factory, organizationId, AuditOutcome.Denied, "tenancy.demo.fail", DateTimeOffset.UtcNow.AddDays(-30));

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId:D}/health");
        var body = await response.Content.ReadFromJsonAsync<OrganizationHealthDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(0, body.RecentErrorCount);
        Assert.Empty(body.RecentErrors);
    }

    [Fact]
    public async Task GetHealth_WithUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/health");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_WithoutPermission_Returns403AndAuditsDenied()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/health");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.ViewOrganizationHealth);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task GetHealth_WritesSucceededAuditWithSummary()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);
        await SeedDeviceAsync(factory, organizationId);

        var response = await client.GetAsync($"/api/platform/organizations/{organizationId:D}/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == AuditActionNames.ViewOrganizationHealth && record.Outcome == AuditOutcome.Succeeded)
            .SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Contains("\"BranchCount\":1", audit.DetailsJson);
        Assert.Contains("\"DeviceCount\":1", audit.DetailsJson);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            new CreateOrganizationRequest(
                OrganizationSlug: $"demo-{Guid.NewGuid():N}".Substring(0, 30),
                OrganizationName: "Demo Club",
                BranchSlug: "main",
                BranchName: "Main Branch",
                BranchCity: "Dushanbe",
                PlanCode: OrganizationPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new OrganizationLimitsDto(1, 20, 30, 5),
                OwnerUserName: null,
                OwnerDisplayName: null,
                OrganizationOwnerInviteLifetime: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return body.Organization.OrganizationId;
    }

    private static async Task SeedDeviceAsync(PlatformApiFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await dbContext.Branches.SingleAsync(b => b.OrganizationId == organizationId);
        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branch.BranchId,
            MachineName = "PC-01",
            AgentVersion = "1.0",
            ShellVersion = "1.0",
            EnrolledAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStaffSignInAsync(PlatformApiFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.StaffAccessTokens.Add(new StaffAccessTokenEntity
        {
            StaffAccessTokenId = Guid.NewGuid(),
            StaffUserId = Guid.NewGuid(),
            OrganizationId = organizationId,
            TokenHash = [1, 2, 3],
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8)
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedAuditAsync(
        PlatformApiFactory factory,
        Guid organizationId,
        string outcome,
        string action,
        DateTimeOffset createdAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = null,
            ActorStaffUserId = null,
            Action = action,
            TargetType = "Test",
            TargetId = null,
            Outcome = outcome,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = createdAtUtc
        });
        await dbContext.SaveChangesAsync();
    }
}
