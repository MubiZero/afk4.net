using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessSessionEndpointTests
{
    [Fact]
    public async Task RedeemTicket_ReturnsSessionOnce()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var ticket = await SupportAccessTestHelper.IssueTicketAsync(factory);

        var first = await client.PostAsJsonAsync(
            "/api/public/support-access/sessions", new RedeemSupportAccessTicketRequest(ticket));
        var second = await client.PostAsJsonAsync(
            "/api/public/support-access/sessions", new RedeemSupportAccessTicketRequest(ticket));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);

        var session = await first.Content.ReadFromJsonAsync<PlatformSupportSessionDto>();
        Assert.NotNull(session);
        Assert.Contains(PlatformSupportWritableAreas.BranchSettings, session!.WritableAreas);
    }

    // A page reload under an active support session re-fetches its own session state — it must land
    // in the same shape the initial redeem returned (branches included), not a thinner one the shell
    // doesn't know how to render.
    [Fact]
    public async Task GetSession_ReturnsTheSameShapeAsRedeem_IncludingBranches()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId, _) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.GetAsync("/api/support-access/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<PlatformSupportSessionDto>();
        Assert.NotNull(session);
        Assert.Equal(organizationId, session!.OrganizationId);
        Assert.Equal(sessionToken, session.SessionToken);
        Assert.Contains(session.Branches, branch => branch.BranchId == branchId);
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId, _) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var signOut = await client.DeleteAsync("/api/support-access/session");
        var afterSignOut = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }

    // Regression: the manual old-mechanism branches in DiagnosticsEndpoints/OrganizationAuditEndpoints
    // used to fall back to StaffUserId = Guid.Empty under a support session because the support context
    // never reached the shared audit path — attribution to the platform admin was lost. Now that both
    // endpoints go through the ordinary RequireBranchPermissionAsync/RequireOrganizationPermission path,
    // AuditRecordStager must pick up StaffContext.SupportAccess and stamp ActorPlatformAdminUserId.
    //
    // Attribution alone isn't enough either: without the grant id and reason sitting in the record
    // itself, matching an action back to the client request that justified it means eyeballing
    // timestamps against the separate grant-issued record — ambiguous the moment two grants for the
    // same organization overlap. AuditRecordStager must fold both into DetailsJson.supportAccess.
    [Fact]
    public async Task ViewAudit_UnderSupportSession_RecordsGrantAndReasonAlongsideAttribution()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        const string reason = "Клиент не видит журнал аудита филиала";
        var (sessionToken, organizationId, _, platformAdminUserId) =
            await SupportAccessTestHelper.OpenSessionAsync(factory, reason);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        // The organization-scoped audit route carries AllowPlatformSupportAccess; the branch-scoped
        // one deliberately doesn't (Task 7 brief only marks the org-level route for support access).
        var response = await client.GetAsync($"/api/organizations/{organizationId}/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var grant = await dbContext.PlatformSupportAccessGrants
            .SingleAsync(candidate => candidate.OrganizationId == organizationId);
        var record = dbContext.AuditRecords
            .Where(x => x.OrganizationId == organizationId && x.Action == "audit.view")
            .OrderByDescending(x => x.CreatedAtUtc)
            .First();

        Assert.Equal(platformAdminUserId, record.ActorPlatformAdminUserId);
        Assert.Null(record.ActorStaffUserId);

        using var details = JsonDocument.Parse(record.DetailsJson);
        var supportAccess = details.RootElement.GetProperty("supportAccess");
        Assert.Equal(grant.GrantId, supportAccess.GetProperty("grantId").GetGuid());
        Assert.Equal(reason, supportAccess.GetProperty("reason").GetString());
        // The operational field the endpoint itself writes (count of returned audit records) must
        // survive the merge — the fix is additive, not a replacement of what was already there.
        Assert.True(details.RootElement.TryGetProperty("Scope", out _));
    }

    // Mirror of the test above for the ordinary (non-support) path: an org staff member's audit
    // record must NOT grow a supportAccess block — that field means "a platform admin acted here
    // under a grant", and it would be actively misleading on a staff member's own action.
    [Fact]
    public async Task ViewAudit_UnderOrdinaryStaffSession_HasNoSupportAccessDetails()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(
            factory, client, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = dbContext.AuditRecords
            .Where(x => x.OrganizationId == TestIds.OrganizationId && x.Action == "audit.view")
            .OrderByDescending(x => x.CreatedAtUtc)
            .First();

        Assert.NotNull(record.ActorStaffUserId);
        Assert.Null(record.ActorPlatformAdminUserId);
        using var details = JsonDocument.Parse(record.DetailsJson);
        Assert.False(details.RootElement.TryGetProperty("supportAccess", out _));
    }
}
