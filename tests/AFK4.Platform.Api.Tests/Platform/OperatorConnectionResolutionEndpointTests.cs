using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.Organizations;
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
        var organizationId = await CreateOrganizationAsync(client, "demo-club", "main");

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
        Assert.Equal(OrganizationStatusNames.Active, body.OrganizationStatus);
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
        await CreateOrganizationAsync(client, "demo-club", "main");
        var organizationOwnerInvite = await GetLatestInviteAsync(factory);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, organizationOwnerInvite.Code));
        var body = await response.Content.ReadFromJsonAsync<ResolveOperatorConnectionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(organizationOwnerInvite.OrganizationId, body.OrganizationId);
        Assert.Equal(organizationOwnerInvite.BranchId, body.BranchId);
        Assert.Equal(OperatorConnectionResolutionSources.SetupCode, body.Source);
    }

    [Fact]
    public async Task PostResolve_OnSuspendedOrganization_StillResolvesWithSuspendedStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "demo-club", "main");

        var suspend = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, "Unpaid invoice"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest("demo-club", "main", null));
        var body = await response.Content.ReadFromJsonAsync<ResolveOperatorConnectionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(OrganizationStatusNames.Suspended, body.OrganizationStatus);
        Assert.Equal("Unpaid invoice", body.OrganizationStatusReason);
    }

    [Fact]
    public async Task PostResolve_WithRevokedInvite_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        await CreateOrganizationAsync(client, "demo-club", "main");
        var invite = await GetLatestInviteAsync(factory);

        var revoke = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{invite.OrganizationOwnerInviteId:D}/revoke",
            new RevokeOrganizationOwnerInviteRequest("Test"));
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
        await CreateOrganizationAsync(client, "demo-club", "main");
        var invite = await GetLatestInviteAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var entity = await dbContext.OrganizationOwnerInvites.SingleAsync(i => i.OrganizationOwnerInviteId == invite.OrganizationOwnerInviteId);
            entity.Status = OrganizationOwnerInviteStatusNames.Expired;
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
        await CreateOrganizationAsync(client, "demo-club", "main");

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
        await CreateOrganizationAsync(client, "demo-club", "main");
        var invite = await GetLatestInviteAsync(factory);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/operator-connections/resolve",
            new ResolveOperatorConnectionRequest(null, null, invite.Code.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client, string orgSlug, string branchSlug)
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            new CreateOrganizationRequest(
                OrganizationSlug: orgSlug,
                OrganizationName: "Demo Club",
                BranchSlug: branchSlug,
                BranchName: "Main Branch",
                BranchCity: "Dushanbe",
                PlanCode: OrganizationPlanCodeNames.Starter,
                SubscriptionStatus: SubscriptionStatusNames.Trial,
                Limits: new OrganizationLimitsDto(1, 20, 30, 5),
                OwnerUserName: "owner@demo-club.test",
                OwnerDisplayName: "Demo Owner",
                OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return body.Organization.OrganizationId;
    }

    private static async Task<OrganizationOwnerInviteDto> GetLatestInviteAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entity = await dbContext.OrganizationOwnerInvites
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .FirstAsync();
        return new OrganizationOwnerInviteDto(
            OrganizationOwnerInviteId: entity.OrganizationOwnerInviteId,
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
