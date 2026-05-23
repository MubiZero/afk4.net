using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Invites;
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
            OwnerInviteLifetime: TimeSpan.FromDays(7));
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
        Assert.Equal(OwnerInviteStatusNames.Pending, body.OwnerInvite.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.OwnerInvite.Code));
        Assert.Equal("owner@demo-club.test", body.OwnerInvite.OwnerUserName);
        Assert.Equal(body.Tenant.Branches[0].BranchId, body.OwnerInvite.BranchId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync();
        Assert.Equal("demo-club", organization.Slug);
        Assert.Equal(TenantStatusNames.Active, organization.Status);

        var branch = await dbContext.Branches.SingleAsync();
        Assert.Equal("demo-branch", branch.Slug);
        Assert.Equal(organization.OrganizationId, branch.OrganizationId);

        var invite = await dbContext.OwnerInvites.SingleAsync();
        Assert.Equal(OwnerInviteStatusNames.Pending, invite.Status);
        Assert.Equal(admin.PlatformAdminId, invite.CreatedByPlatformAdminUserId);

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.tenant.create");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(organization.OrganizationId, audit.OrganizationId);
        Assert.Equal(organization.OrganizationId.ToString("D"), audit.TargetId);
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
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Organizations.ToListAsync());
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.tenant.create");
        Assert.Equal("Denied", audit.Outcome);
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
    public async Task PostOwnerInvites_RotatesPendingInvitesAndReturnsFresh()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);
        var originalInvite = created.OwnerInvite;
        var branchId = created.Tenant.Branches[0].BranchId;

        var rotateRequest = new CreateOwnerInviteRequest(
            BranchId: branchId,
            OwnerUserName: "owner2@demo-club.test",
            OwnerDisplayName: "Replacement Owner",
            Lifetime: TimeSpan.FromDays(14));
        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/owner-invites",
            rotateRequest);
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<OwnerInviteDto>();

        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        Assert.NotNull(rotated);
        Assert.NotEqual(originalInvite.OwnerInviteId, rotated.OwnerInviteId);
        Assert.NotEqual(originalInvite.Code, rotated.Code);
        Assert.Equal(OwnerInviteStatusNames.Pending, rotated.Status);
        Assert.Equal("owner2@demo-club.test", rotated.OwnerUserName);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var invites = await dbContext.OwnerInvites.OrderBy(inv => inv.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, invites.Count);
        Assert.Equal(OwnerInviteStatusNames.Revoked, invites[0].Status);
        Assert.NotNull(invites[0].RevokedAtUtc);
        Assert.Equal("Rotated by platform admin.", invites[0].RevokedReason);
        Assert.Equal(OwnerInviteStatusNames.Pending, invites[1].Status);
    }

    [Fact]
    public async Task PostOwnerInvites_WithUnknownBranch_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/owner-invites",
            new CreateOwnerInviteRequest(Guid.NewGuid(), null, null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WithValidCode_CreatesOwnerStaffAndReturnsSignIn()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        using var publicClient = factory.CreateClient();
        var acceptResponse = await publicClient.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(
                Code: created.OwnerInvite.Code,
                UserName: "demo.owner",
                DisplayName: "Demo Owner",
                Password: "Passw0rd!Real"));
        var signIn = await acceptResponse.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.NotNull(signIn);
        Assert.Equal(created.Tenant.OrganizationId, signIn.OrganizationId);
        Assert.Contains(created.Tenant.Branches[0].BranchId, signIn.BranchIds);
        Assert.Contains(StaffPermissionNames.ViewFloorMap, signIn.Permissions);
        Assert.Contains(StaffPermissionNames.ManageBranchStaff, signIn.Permissions);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = await dbContext.StaffUsers.SingleAsync();
        Assert.Equal("demo.owner", staff.UserName);
        Assert.Equal("DEMO.OWNER", staff.NormalizedUserName);
        Assert.True(staff.IsActive);

        var assignment = await dbContext.StaffRoleAssignments.SingleAsync();
        Assert.Equal(StaffRoleNames.Owner, assignment.RoleName);
        Assert.Equal(created.Tenant.Branches[0].BranchId, assignment.BranchId);

        var invite = await dbContext.OwnerInvites.SingleAsync();
        Assert.Equal(OwnerInviteStatusNames.Accepted, invite.Status);
        Assert.Equal(staff.StaffUserId, invite.AcceptedByStaffUserId);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WithUnknownCode_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest("ghost-code", "owner", "Owner", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WithExpiredInvite_MarksExpiredAndReturns400()
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
            var invite = await dbContext.OwnerInvites.SingleAsync();
            invite.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
        }

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(created.OwnerInvite.Code, "owner", "Owner", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext2 = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var inviteAfter = await dbContext2.OwnerInvites.SingleAsync();
        Assert.Equal(OwnerInviteStatusNames.Expired, inviteAfter.Status);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WithRevokedInvite_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        // Rotate to revoke the original invite.
        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/owner-invites",
            new CreateOwnerInviteRequest(created.Tenant.Branches[0].BranchId, null, null, null));
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(created.OwnerInvite.Code, "owner", "Owner", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WithDuplicateUserName_Returns409()
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
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(created.OwnerInvite.Code, "demo.owner", "Demo Owner", "Passw0rd!Real"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WithShortPassword_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(created.OwnerInvite.Code, "owner", "Owner", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StaffAccessToken_CannotReachPlatformTenantEndpoints()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
        {
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, StaffRoleNames.Owner);
        }

        using var staffClient = factory.CreateClient();
        var signIn = await staffClient.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));
        var signInBody = await signIn.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(signInBody);
        staffClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signInBody.AccessToken);

        var response = await staffClient.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
