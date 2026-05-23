using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class OperatorConnectionResolutionEndpointTests
{
    [Fact]
    public async Task PostResolve_WithValidSlugPair_ReturnsConnectionMetadataAndAudits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client, "demo-club", "main");

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("demo-club", "main", null));
        var body = await response.Content.ReadFromJsonAsync<ResolveOperatorConnectionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(organizationId, body.OrganizationId);
        Assert.Equal("demo-club", body.OrganizationSlug);
        Assert.Equal("Demo Club", body.OrganizationName);
        Assert.Equal(TenantStatusNames.Active, body.OrganizationStatus);
        Assert.Equal("main", body.BranchSlug);
        Assert.Equal("Main Branch", body.BranchName);
        Assert.Equal("Dushanbe", body.BranchCity);
        Assert.Equal(OperatorConnectionResolutionSources.Slug, body.Source);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == AuditActionNames.ResolveOperatorConnection && record.Outcome == AuditOutcome.Succeeded)
            .SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Contains("\"OrganizationSlug\":\"demo-club\"", audit.DetailsJson);
    }

    [Fact]
    public async Task PostResolve_WithSetupCode_ReturnsMetadataAndAudits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await CreateTenantAsync(client, "demo-club", "main");
        var ownerInvite = await GetLatestInviteAsync(factory);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, ownerInvite.Code));
        var body = await response.Content.ReadFromJsonAsync<ResolveOperatorConnectionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ownerInvite.OrganizationId, body.OrganizationId);
        Assert.Equal(ownerInvite.BranchId, body.BranchId);
        Assert.Equal(OperatorConnectionResolutionSources.SetupCode, body.Source);
    }

    [Fact]
    public async Task PostResolve_OnSuspendedTenant_StillResolvesWithSuspendedStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client, "demo-club", "main");

        var suspend = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, "Unpaid invoice"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("demo-club", "main", null));
        var body = await response.Content.ReadFromJsonAsync<ResolveOperatorConnectionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TenantStatusNames.Suspended, body.OrganizationStatus);
        Assert.Equal("Unpaid invoice", body.OrganizationStatusReason);
    }

    [Fact]
    public async Task PostResolve_WithRevokedInvite_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await CreateTenantAsync(client, "demo-club", "main");
        var invite = await GetLatestInviteAsync(factory);

        var revoke = await client.PostAsJsonAsync(
            $"/api/platform/owner-invites/{invite.OwnerInviteId:D}/revoke",
            new RevokeOwnerInviteRequest("Test"));
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, invite.Code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostResolve_WithExpiredInvite_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await CreateTenantAsync(client, "demo-club", "main");
        var invite = await GetLatestInviteAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var entity = await dbContext.OwnerInvites.SingleAsync(i => i.OwnerInviteId == invite.OwnerInviteId);
            entity.Status = OwnerInviteStatusNames.Expired;
            await dbContext.SaveChangesAsync();
        }

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, invite.Code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostResolve_WithUnknownSlug_Returns404AndAuditsDenied()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("ghost-club", "main", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == AuditActionNames.ResolveOperatorConnection && record.Outcome == AuditOutcome.Denied)
            .SingleAsync();
        Assert.Contains("\"HasSlugPair\":true", audit.DetailsJson);
    }

    [Fact]
    public async Task PostResolve_WithKnownOrgButUnknownBranchSlug_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await CreateTenantAsync(client, "demo-club", "main");

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("demo-club", "ghost-branch", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostResolve_WithBothSlugAndSetupCode_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("demo-club", "main", "code-xyz"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostResolve_WithNoFields_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostResolve_WithInvalidSlugFormat_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("under_score", "main", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostResolve_AcceptsCaseInsensitiveSetupCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await CreateTenantAsync(client, "demo-club", "main");
        var invite = await GetLatestInviteAsync(factory);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, invite.Code.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string orgSlug, string branchSlug)
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            new CreateTenantRequest(
                OrganizationSlug: orgSlug,
                OrganizationName: "Demo Club",
                BranchSlug: branchSlug,
                BranchName: "Main Branch",
                BranchCity: "Dushanbe",
                PlanCode: TenantPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new TenantLimitsDto(1, 20, 30, 5),
                OwnerUserName: "owner@demo-club.test",
                OwnerDisplayName: "Demo Owner",
                OwnerInviteLifetime: TimeSpan.FromDays(7)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(body);
        return body.Tenant.OrganizationId;
    }

    private static async Task<OwnerInviteDto> GetLatestInviteAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entity = await dbContext.OwnerInvites
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .FirstAsync();
        return new OwnerInviteDto(
            OwnerInviteId: entity.OwnerInviteId,
            OrganizationId: entity.OrganizationId,
            BranchId: entity.BranchId,
            Code: entity.Code,
            Status: entity.Status,
            OwnerUserName: entity.OwnerUserName,
            OwnerDisplayName: entity.OwnerDisplayName,
            ExpiresAtUtc: entity.ExpiresAtUtc,
            AcceptedAtUtc: entity.AcceptedAtUtc,
            RevokedAtUtc: entity.RevokedAtUtc,
            RevokedReason: entity.RevokedReason,
            CreatedAtUtc: entity.CreatedAtUtc);
    }
}
