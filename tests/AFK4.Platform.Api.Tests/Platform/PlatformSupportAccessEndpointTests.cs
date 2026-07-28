using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Support;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessEndpointTests
{
    [Fact]
    public async Task ExpiredGrant_IsRejectedServerSide()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var created = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate expired support workflow", 1));
        var grant = await created.Content.ReadFromJsonAsync<PlatformSupportAccessGrantDto>();
        Assert.NotNull(grant);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.PlatformSupportAccessGrants.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-AFK4-Support-Access-Grant", grant.GrantId.ToString("D"));

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Grant_UsesOnlyAllowlistedReadForBoundOrganizationAndStopsAfterRevocation()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var created = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate organization audit failure", 30));
        var grant = await created.Content.ReadFromJsonAsync<PlatformSupportAccessGrantDto>();
        Assert.NotNull(grant);
        client.DefaultRequestHeaders.Add("X-AFK4-Support-Access-Grant", grant.GrantId.ToString("D"));

        var allowed = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");
        var diagnostics = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/diagnostics");
        var crossOrganization = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/audit");
        var mutation = await client.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(false, 0, false, 0, false, 0, 0, 0));
        var revoked = await client.DeleteAsync($"/api/platform/support-access-grants/{grant.GrantId:D}");
        var afterRevocation = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, diagnostics.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossOrganization.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mutation.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, afterRevocation.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var useAudits = await db.AuditRecords.Where(record => record.Action == "platform.support_access.use").ToListAsync();
        Assert.Equal(2, useAudits.Count);
        Assert.All(useAudits, useAudit =>
        {
            Assert.Equal(admin.PlatformAdminId, useAudit.ActorPlatformAdminUserId);
            Assert.Equal(TestIds.OrganizationId, useAudit.OrganizationId);
            Assert.Contains(grant.GrantId.ToString("D"), useAudit.DetailsJson);
        });
    }

    [Fact]
    public async Task CreateGrant_RequiresReasonAndCapsLifetimeAtThirtyMinutes()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var blank = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, " ", 30));
        var tooLong = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate device enrollment failure", 31));
        var valid = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate device enrollment failure", 30));
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var body = await valid.Content.ReadFromJsonAsync<PlatformSupportAccessGrantDto>();

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.True(body.ExpiresAtUtc - body.IssuedAtUtc <= TimeSpan.FromMinutes(30));
        Assert.DoesNotContain("token", (await valid.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task RevokeGrant_PersistsRevocationAndAuditsLifecycle()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var created = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate audit export failure", 20));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var grant = await created.Content.ReadFromJsonAsync<PlatformSupportAccessGrantDto>();
        Assert.NotNull(grant);

        var revoked = await client.DeleteAsync($"/api/platform/support-access-grants/{grant.GrantId:D}");

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.NotNull((await db.PlatformSupportAccessGrants.SingleAsync()).RevokedAtUtc);
        var audits = await db.AuditRecords
            .Where(record => record.ActorPlatformAdminUserId == admin.PlatformAdminId)
            .Select(record => record.Action)
            .ToListAsync();
        Assert.Contains("platform.support_access.grant", audits);
        Assert.Contains("platform.support_access.revoke", audits);
    }
}
