using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Support;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessEndpointTests
{
    // A grant that has expired can no longer be exchanged for a working session: RedeemTicketAsync
    // only rejects an unclaimed ticket up front, so this exercises the later gate — a session token
    // that was minted while the grant was live stops authenticating once the grant's own expiry
    // passes. The session middleware treats an unrecognized/expired token as a rejected credential.
    [Fact]
    public async Task ExpiredGrant_SessionStopsAuthenticatingAfterExpiry()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var created = await client.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate expired support workflow", 1));
        var issue = await created.Content.ReadFromJsonAsync<PlatformSupportAccessGrantIssue>();
        Assert.NotNull(issue);

        var redeemed = await client.PostAsJsonAsync(
            "/api/public/support-access/sessions", new RedeemSupportAccessTicketRequest(issue!.Ticket));
        var session = await redeemed.Content.ReadFromJsonAsync<PlatformSupportSessionDto>();
        Assert.NotNull(session);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.PlatformSupportAccessGrants.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, session!.SessionToken);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_UsesOnlyAllowlistedReadForBoundOrganizationAndStopsAfterRevocation()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        // Grant management (issue/revoke) is the platform admin's own bearer-authenticated tooling and
        // never carries the support-session header; the session header lives on a separate client, the
        // same way a real support agent's org-admin browser tab is a different client than the platform
        // console tab that issued the grant.
        using var adminClient = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, adminClient, roles: [PlatformAdminRoleNames.PlatformSupport]);
        var created = await adminClient.PostAsJsonAsync("/api/platform/support-access-grants",
            new CreatePlatformSupportAccessGrantRequest(TestIds.OrganizationId, "Investigate organization audit failure", 30));
        var issue = await created.Content.ReadFromJsonAsync<PlatformSupportAccessGrantIssue>();
        Assert.NotNull(issue);
        using var client = factory.CreateClient();
        var redeemed = await client.PostAsJsonAsync(
            "/api/public/support-access/sessions", new RedeemSupportAccessTicketRequest(issue!.Ticket));
        var session = await redeemed.Content.ReadFromJsonAsync<PlatformSupportSessionDto>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, session!.SessionToken);

        var allowed = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");
        var diagnostics = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/diagnostics");
        var crossOrganization = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/audit");
        // Not allowlisted for support access, so the session middleware refuses it outright.
        var mutation = await client.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(false, 0, false, 0, false, 0, 0, 0));
        var revoked = await adminClient.DeleteAsync($"/api/platform/support-access-grants/{issue.Grant.GrantId:D}");
        var afterRevocation = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, diagnostics.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossOrganization.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mutation.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);

        // The two successful reads must go through the ordinary audit path (audit.view/diagnostics.view)
        // and land attributed to the platform admin, not a nameless staff user — this is what the shared
        // AuditRecordStager guarantees once the endpoints stopped hand-rolling their own support branch.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var supportAttributedAudits = await db.AuditRecords
            .Where(record => record.OrganizationId == TestIds.OrganizationId
                && record.ActorPlatformAdminUserId == admin.PlatformAdminId)
            .ToListAsync();
        Assert.Contains(supportAttributedAudits, record => record.Action == "audit.view");
        Assert.Contains(supportAttributedAudits, record => record.Action == "diagnostics.view");
        Assert.All(supportAttributedAudits, record => Assert.Null(record.ActorStaffUserId));
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
        var body = await valid.Content.ReadFromJsonAsync<PlatformSupportAccessGrantIssue>();

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.Grant.OrganizationId);
        Assert.True(body.Grant.ExpiresAtUtc - body.Grant.IssuedAtUtc <= TimeSpan.FromMinutes(30));
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
        var issue = await created.Content.ReadFromJsonAsync<PlatformSupportAccessGrantIssue>();
        Assert.NotNull(issue);

        var revoked = await client.DeleteAsync($"/api/platform/support-access-grants/{issue!.Grant.GrantId:D}");

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
