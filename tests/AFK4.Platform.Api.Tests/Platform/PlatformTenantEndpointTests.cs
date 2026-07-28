using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformTenantEndpointTests
{
    private static CreateTenantRequest BuildCreateTenantRequest(
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch",
        string? ownerUserName = "owner@demo-club.test")
    {
        return new CreateTenantRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Demo Club",
            BranchSlug: branchSlug,
            BranchName: "Demo Branch",
            BranchCity: "Dushanbe",
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new TenantLimitsDto(MaxBranches: 3, MaxDevicesPerBranch: 60, MaxConcurrentSessions: 80, MaxStaffUsersPerBranch: 20),
            OwnerUserName: ownerUserName,
            OwnerDisplayName: "Demo Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task PostTenants_WithValidRequest_PersistsTenantBranchInviteAndReturnsResponse()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = BuildCreateTenantRequest();
        var response = await client.PostAsJsonAsync("/api/platform/tenants", request);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("demo-club", body.Tenant.Slug);
        Assert.Equal("Demo Club", body.Tenant.Name);
        Assert.Equal(TenantStatusNames.Active, body.Tenant.Status);
        Assert.Equal(TenantPlanCodeNames.Starter, body.Tenant.PlanCode);
        Assert.Equal(SubscriptionStatusNames.Trial, body.Tenant.SubscriptionStatus);
        Assert.Single(body.Tenant.Branches);
        Assert.Equal("demo-branch", body.Tenant.Branches[0].Slug);
        Assert.Equal("Dushanbe", body.Tenant.Branches[0].City);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, body.OrganizationOwnerInvite.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.OrganizationOwnerInvite.Code));
        Assert.Equal("owner@demo-club.test", body.OrganizationOwnerInvite.OwnerUserName);
        Assert.Equal(body.Tenant.Branches[0].BranchId, body.OrganizationOwnerInvite.BranchId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync();
        Assert.Equal("demo-club", organization.Slug);
        Assert.Equal(TenantStatusNames.Active, organization.Status);

        var branch = await dbContext.Branches.SingleAsync();
        Assert.Equal("demo-branch", branch.Slug);
        Assert.Equal(organization.OrganizationId, branch.OrganizationId);

        var invite = await dbContext.OrganizationOwnerInvites.SingleAsync();
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, invite.Status);
        Assert.Equal(admin.PlatformAdminId, invite.CreatedByPlatformAdminUserId);

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.tenant.create");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(organization.OrganizationId, audit.OrganizationId);
        Assert.Equal(organization.OrganizationId.ToString("D"), audit.TargetId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
        Assert.DoesNotContain("actorPlatformAdminUserId", audit.DetailsJson);
    }

    [Fact]
    public async Task PostTenants_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTenants_WithSupportRoleOnly_ReturnsForbiddenAndAuditsDenied()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Organizations.ToListAsync());
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.tenant.create");
        Assert.Equal("Denied", audit.Outcome);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task PostTenants_WithInvalidSlug_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = BuildCreateTenantRequest(orgSlug: "Has Space");
        var response = await client.PostAsJsonAsync("/api/platform/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTenants_WithDuplicateOrgSlug_Returns409AndDoesNotDuplicate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var first = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            BuildCreateTenantRequest(branchSlug: "second-branch"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
        Assert.Single(await dbContext.Branches.ToListAsync());
    }

    [Fact]
    public async Task PostTenants_WithUnknownSubscriptionStatus_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = BuildCreateTenantRequest() with { SubscriptionStatus = "ghost_status" };
        var response = await client.PostAsJsonAsync("/api/platform/tenants", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTenants_ReturnsAllOrganizationSummariesAlphabetically()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var first = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            BuildCreateTenantRequest(orgSlug: "z-last", branchSlug: "main", ownerUserName: null) with { OrganizationName = "Zeta Club" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync(
            "/api/platform/tenants",
            BuildCreateTenantRequest(orgSlug: "a-first", branchSlug: "main", ownerUserName: null) with { OrganizationName = "Alpha Club" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var response = await client.GetAsync("/api/platform/tenants");
        var body = await response.Content.ReadFromJsonAsync<List<TenantSummaryDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
        Assert.Equal("Alpha Club", body[0].Name);
        Assert.Equal("Zeta Club", body[1].Name);
        Assert.All(body, summary => Assert.Equal(1, summary.BranchCount));
    }

    [Fact]
    public async Task GetTenantById_ReturnsDetailWithBranchesAndParsedLimits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var detailResponse = await client.GetAsync($"/api/platform/tenants/{created.Tenant.OrganizationId:D}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<TenantDetailDto>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.Limits.MaxBranches);
        Assert.Equal(60, detail.Limits.MaxDevicesPerBranch);
        Assert.Equal(80, detail.Limits.MaxConcurrentSessions);
        Assert.Equal(20, detail.Limits.MaxStaffUsersPerBranch);
        Assert.Single(detail.Branches);
    }

    [Fact]
    public async Task GetTenantById_WithUnknownId_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationOwnerInvites_RotatesPendingInvitesAndReturnsFresh()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);
        var originalInvite = created.OrganizationOwnerInvite;
        var branchId = created.Tenant.Branches[0].BranchId;

        var rotateRequest = new CreateOrganizationOwnerInviteRequest(
            BranchId: branchId,
            OwnerUserName: "owner2@demo-club.test",
            OwnerDisplayName: "Replacement Owner",
            Lifetime: TimeSpan.FromDays(14));
        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/organization-owner-invitations",
            rotateRequest);
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();

        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        Assert.NotNull(rotated);
        Assert.NotEqual(originalInvite.OrganizationOwnerInviteId, rotated.OrganizationOwnerInviteId);
        Assert.NotEqual(originalInvite.Code, rotated.Code);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, rotated.Status);
        Assert.Equal("owner2@demo-club.test", rotated.OwnerUserName);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var invites = await dbContext.OrganizationOwnerInvites.OrderBy(inv => inv.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, invites.Count);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Revoked, invites[0].Status);
        Assert.NotNull(invites[0].RevokedAtUtc);
        Assert.Equal("Rotated by platform admin.", invites[0].RevokedReason);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, invites[1].Status);
    }

    [Fact]
    public async Task PostOrganizationOwnerInvites_WithUnknownBranch_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(Guid.NewGuid(), null, null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostAccountActivation_WithValidCode_CreatesOwnerStaffWithoutIssuingTokens()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        using var publicClient = factory.CreateClient();
        var acceptResponse = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(
                Code: created.OrganizationOwnerInvite.Code,
                UserName: "demo.owner",
                DisplayName: "Demo Owner",
                Password: "Passw0rd!Real"));
        var json = await acceptResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.Equal(created.Tenant.OrganizationId, json.GetProperty("organizationId").GetGuid());
        Assert.Equal("sign_in_to_organization_admin", json.GetProperty("nextStep").GetString());
        Assert.False(json.TryGetProperty("accessToken", out _));
        Assert.False(json.TryGetProperty("refreshToken", out _));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await dbContext.StaffUsers.SingleAsync();
        Assert.Equal("demo.owner", staff.UserName);
        Assert.Equal("DEMO.OWNER", staff.NormalizedUserName);
        Assert.True(staff.IsActive);

        var assignment = await dbContext.StaffRoleAssignments.SingleAsync();
        Assert.Equal(OrganizationRoleNames.OrganizationOwner, assignment.RoleName);
        Assert.Equal(created.Tenant.Branches[0].BranchId, assignment.BranchId);
        Assert.Empty(await dbContext.StaffAccessTokens.ToListAsync());
        Assert.Empty(await dbContext.StaffRefreshTokens.ToListAsync());

        var invite = await dbContext.OrganizationOwnerInvites.SingleAsync();
        Assert.Equal(OrganizationOwnerInviteStatusNames.Accepted, invite.Status);
        Assert.Equal(staff.StaffUserId, invite.AcceptedByStaffUserId);
    }

    [Fact]
    public async Task PostOrganizationOwnerInviteAccept_WithUnknownCode_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest("ghost-code", "owner", "Owner", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationOwnerInviteAccept_WithExpiredInvite_MarksExpiredAndReturns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var invite = await dbContext.OrganizationOwnerInvites.SingleAsync();
            invite.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
        }

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(created.OrganizationOwnerInvite.Code, "owner", "Owner", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext2 = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var inviteAfter = await dbContext2.OrganizationOwnerInvites.SingleAsync();
        Assert.Equal(OrganizationOwnerInviteStatusNames.Expired, inviteAfter.Status);
    }

    [Fact]
    public async Task PostOrganizationOwnerInviteAccept_WithRevokedInvite_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        // Rotate to revoke the original invite.
        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(created.Tenant.Branches[0].BranchId, null, null, null));
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(created.OrganizationOwnerInvite.Code, "owner", "Owner", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationOwnerInviteAccept_WithDuplicateUserName_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        // Pre-create a staff user with the username we will try to claim.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.StaffUsers.Add(new StaffUserEntity
            {
                StaffUserId = Guid.NewGuid(),
                OrganizationId = created.Tenant.OrganizationId,
                UserName = "demo.owner",
                NormalizedUserName = "DEMO.OWNER",
                DisplayName = "Existing",
                PasswordHash = "hash",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(created.OrganizationOwnerInvite.Code, "demo.owner", "Demo Owner", "Passw0rd!Real"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationOwnerInviteAccept_WithShortPassword_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(created.OrganizationOwnerInvite.Code, "owner", "Owner", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptOrganizationOwnerInvite_WithBlankDisplayName_DerivesDisplayNameFromLogin()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(
                Code: created.OrganizationOwnerInvite.Code,
                UserName: "owner@club.test",
                DisplayName: "",
                Password: "Passw0rd!Real"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal("owner", (await dbContext.StaffUsers.SingleAsync()).DisplayName);
    }

    [Fact]
    public async Task RevokeOrganizationOwnerInvite_WithValidPendingInvite_MarksRevokedAndAudits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var revoke = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{created.OrganizationOwnerInvite.OrganizationOwnerInviteId:D}/revoke",
            new RevokeOrganizationOwnerInviteRequest("Owner asked to cancel"));
        var revoked = await revoke.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.NotNull(revoked);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Revoked, revoked.Status);
        Assert.Equal("Owner asked to cancel", revoked.RevokedReason);
        Assert.NotNull(revoked.RevokedAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var invite = await dbContext.OrganizationOwnerInvites.SingleAsync(i => i.OrganizationOwnerInviteId == created.OrganizationOwnerInvite.OrganizationOwnerInviteId);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Revoked, invite.Status);
        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "tenancy.owner_invite.revoke" && record.Outcome == "Succeeded")
            .SingleAsync();
        Assert.Equal(created.Tenant.OrganizationId, audit.OrganizationId);
        Assert.Equal(created.OrganizationOwnerInvite.OrganizationOwnerInviteId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task RevokeOrganizationOwnerInvite_WithUnknownInvite_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{Guid.NewGuid():D}/revoke",
            new RevokeOrganizationOwnerInviteRequest("anyway"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeOrganizationOwnerInvite_OnAlreadyRevokedInvite_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var first = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{created.OrganizationOwnerInvite.OrganizationOwnerInviteId:D}/revoke",
            new RevokeOrganizationOwnerInviteRequest("First"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{created.OrganizationOwnerInvite.OrganizationOwnerInviteId:D}/revoke",
            new RevokeOrganizationOwnerInviteRequest("Again"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task RevokeOrganizationOwnerInvite_WithoutReason_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{created.OrganizationOwnerInvite.OrganizationOwnerInviteId:D}/revoke",
            new RevokeOrganizationOwnerInviteRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevokeOrganizationOwnerInvite_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organization-owner-invitations/{Guid.NewGuid():D}/revoke",
            new RevokeOrganizationOwnerInviteRequest("test"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTenants_WithIdempotencyKey_StoresAndReplaysOnRetry()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        const string idempotencyKey = "tenant-create-attempt-001";
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/platform/tenants")
        {
            Content = JsonContent.Create(BuildCreateTenantRequest())
        };
        first.Headers.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.SendAsync(first);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.False(firstResponse.Headers.Contains("Idempotency-Replayed"));

        using var retry = new HttpRequestMessage(HttpMethod.Post, "/api/platform/tenants")
        {
            Content = JsonContent.Create(BuildCreateTenantRequest())
        };
        retry.Headers.Add("Idempotency-Key", idempotencyKey);
        var retryResponse = await client.SendAsync(retry);
        var retryBody = await retryResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.True(retryResponse.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal("true", retryResponse.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.NotNull(retryBody);
        Assert.Equal(firstBody.Tenant.OrganizationId, retryBody.Tenant.OrganizationId);
        Assert.Equal(firstBody.OrganizationOwnerInvite.Code, retryBody.OrganizationOwnerInvite.Code);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
        Assert.Single(await dbContext.PlatformIdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task PostTenants_WithIdempotencyKeyReusedForDifferentBody_Returns422()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        const string idempotencyKey = "tenant-create-attempt-002";
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/platform/tenants")
        {
            Content = JsonContent.Create(BuildCreateTenantRequest(orgSlug: "club-one"))
        };
        first.Headers.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/platform/tenants")
        {
            Content = JsonContent.Create(BuildCreateTenantRequest(orgSlug: "club-two"))
        };
        second.Headers.Add("Idempotency-Key", idempotencyKey);
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
    }

    [Fact]
    public async Task PostTenants_WithoutIdempotencyKey_KeepsSlugBased409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var first = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
        Assert.Empty(await dbContext.PlatformIdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task GetOrganizationOwnerInvites_ReturnsAllInvitesForTenantWithMaskedCodes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(created.Tenant.Branches[0].BranchId, OwnerUserName: "owner-2@demo-club.test", OwnerDisplayName: "Owner Two", Lifetime: TimeSpan.FromDays(7)));
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();
        Assert.NotNull(rotated);

        var listResponse = await client.GetAsync($"/api/platform/tenants/{created.Tenant.OrganizationId:D}/organization-owner-invitations");
        var invites = await listResponse.Content.ReadFromJsonAsync<List<OrganizationOwnerInviteSummaryDto>>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(invites);
        Assert.Equal(2, invites.Count);

        var rotatedSummary = invites.Single(invite => invite.OrganizationOwnerInviteId == rotated.OrganizationOwnerInviteId);
        Assert.Equal(4, rotatedSummary.CodeSuffix.Length);
        Assert.EndsWith(rotatedSummary.CodeSuffix, rotated.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(rotated.Code, await listResponse.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "tenancy.owner_invite.view" && record.Outcome == "Succeeded")
            .SingleAsync();
        Assert.Equal(created.Tenant.OrganizationId, audit.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task GetOrganizationOwnerInvites_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/organization-owner-invitations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationOwnerInvites_WhenTenantMissing_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/organization-owner-invitations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTenants_SeedsTenantSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.TenantSubscriptions
            .SingleAsync(s => s.OrganizationId == body!.Tenant.OrganizationId);
        Assert.Equal("starter", subscription.PlanCode);
        Assert.Equal(290000, subscription.AmountMinorUnits); // from seeded starter plan
    }

    [Fact]
    public async Task StaffAccessToken_CannotReachPlatformTenantEndpoints()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
        {
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, OrganizationRoleNames.OrganizationOwner);
        }

        using var staffClient = factory.CreateClient();
        var signIn = await staffClient.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));
        var signInBody = await signIn.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(signInBody);
        staffClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signInBody.AccessToken);

        var response = await staffClient.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
