using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationOwnerTransferTests
{
    // Deliberately NOT email-shaped: the login must not double as the owner's real address, otherwise
    // a self-transfer check that (wrongly) compares against UserName would pass the test for the wrong
    // reason. See PostOwnerTransfer_ToSameOwnerAddress_Returns400.
    private const string CurrentOwnerLogin = "current.owner";
    private const string CurrentOwnerEmail = "current-owner@demo-club.test";

    private static CreateOrganizationRequest BuildCreateOrganizationRequest(
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch")
    {
        return new CreateOrganizationRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Demo Club",
            BranchSlug: branchSlug,
            BranchName: "Demo Branch",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new OrganizationLimitsDto(3, 60, 80, 20),
            OwnerUserName: "owner@demo-club.test",
            OwnerDisplayName: "Demo Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
    }

    /// <summary>
    /// Creates an org and accepts its owner invite with a login that does NOT look like an email, but
    /// with a real email attached to the invite (via an admin-issued rotation) so the accepted staff
    /// row ends up with a genuine <see cref="StaffUserEntity.Email"/> on file.
    /// </summary>
    private static async Task<(Guid OrganizationId, Guid BranchId, Guid OwnerStaffUserId)> CreateOrganizationWithActiveOwnerAsync(
        PlatformApiFactory factory,
        HttpClient client,
        string ownerLogin = CurrentOwnerLogin,
        string ownerEmail = CurrentOwnerEmail)
    {
        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{created.Organization.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(
                BranchId: created.Organization.Branches[0].BranchId,
                OwnerUserName: null,
                OwnerDisplayName: null,
                Lifetime: null,
                OwnerEmail: ownerEmail));
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        var rotatedInvite = await rotateResponse.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();
        Assert.NotNull(rotatedInvite);

        using var publicClient = factory.CreateClient();
        var acceptResponse = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(
                Code: rotatedInvite.Code,
                UserName: ownerLogin,
                DisplayName: "Current Owner",
                Password: "Passw0rd!Real"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await dbContext.StaffUsers.SingleAsync(candidate => candidate.OrganizationId == created.Organization.OrganizationId);
        Assert.Equal(ownerEmail, staff.Email);

        return (created.Organization.OrganizationId, created.Organization.Branches[0].BranchId, staff.StaffUserId);
    }

    [Fact]
    public async Task PostOwnerTransfer_CreatesInviteForNewOwnerAndRevokesPreviousOwnerAccess()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId, previousOwnerStaffUserId) = await CreateOrganizationWithActiveOwnerAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/owner-transfer",
            new TransferOrganizationOwnerRequest("new-owner@demo-club.test", "Previous owner left the company"));
        var body = await response.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(branchId, body.BranchId);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, body.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var previousOwner = await dbContext.StaffUsers.SingleAsync(staff => staff.StaffUserId == previousOwnerStaffUserId);
        Assert.False(previousOwner.IsActive);

        var newInvite = await dbContext.OrganizationOwnerInvites
            .SingleAsync(invite => invite.OrganizationOwnerInviteId == body.OrganizationOwnerInviteId);
        Assert.Equal("new-owner@demo-club.test", newInvite.OwnerEmail);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, newInvite.Status);

        var audit = await dbContext.AuditRecords
            .SingleAsync(record => record.Action == "tenancy.organization.owner.transfer" && record.Outcome == "Succeeded");
        Assert.Equal(organizationId, audit.OrganizationId);
    }

    [Fact]
    public async Task PostOwnerTransfer_ToSameOwnerAddress_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, _, previousOwnerStaffUserId) = await CreateOrganizationWithActiveOwnerAsync(factory, client);

        // The submitted address is the owner's real EMAIL, not their (deliberately different) login.
        // This is the scenario the earlier version of this check missed: it only compared against
        // UserName, so a genuine self-transfer by email slipped through as if it were a new owner.
        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/owner-transfer",
            new TransferOrganizationOwnerRequest(CurrentOwnerEmail, "Typo, meant someone else"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var previousOwner = await dbContext.StaffUsers.SingleAsync(staff => staff.StaffUserId == previousOwnerStaffUserId);
        Assert.True(previousOwner.IsActive);
        Assert.Empty(await dbContext.OrganizationOwnerInvites
            .Where(invite => invite.OrganizationId == organizationId && invite.Status == OrganizationOwnerInviteStatusNames.Pending)
            .Where(invite => invite.OwnerEmail == "new-owner@demo-club.test")
            .ToListAsync());
    }

    [Fact]
    public async Task PostOwnerTransfer_ToSameOwnerLogin_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, _, _) = await CreateOrganizationWithActiveOwnerAsync(factory, client);

        // Submitting the owner's LOGIN (not their email) must still be recognised as the same person.
        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/owner-transfer",
            new TransferOrganizationOwnerRequest(CurrentOwnerLogin, "Typo, meant someone else"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOwnerTransfer_WithoutPermission_Returns403AndAuditsDenied()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var (organizationId, _, _) = await CreateOrganizationWithActiveOwnerAsync(factory, client);

        using var supportClient = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory,
            supportClient,
            userName: "support@platform.test",
            roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await supportClient.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/owner-transfer",
            new TransferOrganizationOwnerRequest("new-owner@demo-club.test", "Owner left the company"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .SingleAsync(record => record.Action == "tenancy.organization.owner.transfer" && record.Outcome == "Denied");
        Assert.Equal(organizationId, audit.OrganizationId);
    }

    [Fact]
    public async Task PostOwnerTransfer_WithUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/owner-transfer",
            new TransferOrganizationOwnerRequest("new-owner@demo-club.test", "Owner left the company"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
