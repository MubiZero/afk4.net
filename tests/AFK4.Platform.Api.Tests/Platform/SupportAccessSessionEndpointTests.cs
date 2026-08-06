using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
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
    [Fact]
    public async Task ViewAudit_UnderSupportSession_AttributesToThePlatformAdmin()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, _, platformAdminUserId) =
            await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        // The organization-scoped audit route carries AllowPlatformSupportAccess; the branch-scoped
        // one deliberately doesn't (Task 7 brief only marks the org-level route for support access).
        var response = await client.GetAsync($"/api/organizations/{organizationId}/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = dbContext.AuditRecords
            .Where(x => x.OrganizationId == organizationId && x.Action == "audit.view")
            .OrderByDescending(x => x.CreatedAtUtc)
            .First();

        Assert.Equal(platformAdminUserId, record.ActorPlatformAdminUserId);
        Assert.Null(record.ActorStaffUserId);
    }
}
