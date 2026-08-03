using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Audit;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationEndpointTests
{
    private static CreateOrganizationRequest BuildCreateOrganizationRequest(
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch",
        string? ownerUserName = "owner@demo-club.test")
    {
        return new CreateOrganizationRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Demo Club",
            BranchSlug: branchSlug,
            BranchName: "Demo Branch",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new OrganizationLimitsDto(MaxBranches: 3, MaxDevicesPerBranch: 60, MaxConcurrentSessions: 80, MaxStaffUsersPerBranch: 20),
            OwnerUserName: ownerUserName,
            OwnerDisplayName: "Demo Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task PostOrganizations_WithValidRequest_PersistsOrganizationBranchInviteAndReturnsResponse()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = BuildCreateOrganizationRequest();
        var response = await client.PostAsJsonAsync("/api/platform/organizations", request);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("demo-club", body.Organization.Slug);
        Assert.Equal("Demo Club", body.Organization.Name);
        Assert.Equal(OrganizationStatusNames.Active, body.Organization.Status);
        Assert.Equal(OrganizationPlanCodeNames.Starter, body.Organization.PlanCode);
        Assert.Equal(SubscriptionStatusNames.Trial, body.Organization.SubscriptionStatus);
        Assert.Single(body.Organization.Branches);
        Assert.Equal("demo-branch", body.Organization.Branches[0].Slug);
        Assert.Equal("Dushanbe", body.Organization.Branches[0].City);
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, body.OrganizationOwnerInvite.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.OrganizationOwnerInvite.Code));
        Assert.Equal("owner@demo-club.test", body.OrganizationOwnerInvite.OwnerUserName);
        Assert.Equal(body.Organization.Branches[0].BranchId, body.OrganizationOwnerInvite.BranchId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync();
        Assert.Equal("demo-club", organization.Slug);
        Assert.Equal(OrganizationStatusNames.Active, organization.Status);

        var branch = await dbContext.Branches.SingleAsync();
        Assert.Equal("demo-branch", branch.Slug);
        Assert.Equal(organization.OrganizationId, branch.OrganizationId);

        var invite = await dbContext.OrganizationOwnerInvites.SingleAsync();
        Assert.Equal(OrganizationOwnerInviteStatusNames.Pending, invite.Status);
        Assert.Equal(admin.PlatformAdminId, invite.CreatedByPlatformAdminUserId);

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.organization.create");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(organization.OrganizationId, audit.OrganizationId);
        Assert.Equal(organization.OrganizationId.ToString("D"), audit.TargetId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
        Assert.DoesNotContain("actorPlatformAdminUserId", audit.DetailsJson);
    }

    [Fact]
    public async Task PostOrganizations_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizations_WithSupportRoleOnly_ReturnsForbiddenAndAuditsDenied()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Organizations.ToListAsync());
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tenancy.organization.create");
        Assert.Equal("Denied", audit.Outcome);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task PostOrganizations_WithInvalidSlug_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = BuildCreateOrganizationRequest(orgSlug: "Has Space");
        var response = await client.PostAsJsonAsync("/api/platform/organizations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizations_WithDuplicateOrgSlug_Returns409AndDoesNotDuplicate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var first = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            BuildCreateOrganizationRequest(branchSlug: "second-branch"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
        Assert.Single(await dbContext.Branches.ToListAsync());
    }

    [Fact]
    public async Task PostOrganizations_WithUnknownSubscriptionStatus_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = BuildCreateOrganizationRequest() with { SubscriptionStatus = "ghost_status" };
        var response = await client.PostAsJsonAsync("/api/platform/organizations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizations_ReturnsAllOrganizationSummariesAlphabetically()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var first = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            BuildCreateOrganizationRequest(orgSlug: "z-last", branchSlug: "main", ownerUserName: null) with { OrganizationName = "Zeta Club" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            BuildCreateOrganizationRequest(orgSlug: "a-first", branchSlug: "main", ownerUserName: null) with { OrganizationName = "Alpha Club" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var response = await client.GetAsync("/api/platform/organizations");
        var body = await response.Content.ReadFromJsonAsync<List<OrganizationSummaryDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
        Assert.Equal("Alpha Club", body[0].Name);
        Assert.Equal("Zeta Club", body[1].Name);
        Assert.All(body, summary => Assert.Equal(1, summary.BranchCount));
    }

    [Fact]
    public async Task GetOrganizations_ReturnsOperationalAttentionCountsWithoutPerOrganizationRequests()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var createdResponse = await client.PostAsJsonAsync(
            "/api/platform/organizations", BuildCreateOrganizationRequest(ownerUserName: null));
        var created = await createdResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.AuditRecords.Add(new AuditRecordEntity
            {
                AuditRecordId = Guid.NewGuid(), OrganizationId = created.Organization.OrganizationId,
                Action = "devices.command", TargetType = "Device", Outcome = AuditOutcome.Denied,
                SourceApp = "Agent", DetailsJson = "{}", CreatedAtUtc = now
            });
            db.OrganizationOwnerInvites.Add(new OrganizationOwnerInviteEntity
            {
                OrganizationOwnerInviteId = Guid.NewGuid(), OrganizationId = created.Organization.OrganizationId,
                BranchId = created.Organization.Branches.Single().BranchId, Code = "TEST", NormalizedCode = "TEST",
                Status = OrganizationOwnerInviteStatusNames.Pending, ExpiresAtUtc = now.AddDays(2),
                CreatedByPlatformAdminUserId = Guid.NewGuid(), CreatedAtUtc = now
            });
            db.DeviceUpdateStatuses.Add(new DeviceUpdateStatusEntity
            {
                DeviceUpdateStatusId = Guid.NewGuid(), OrganizationId = created.Organization.OrganizationId,
                BranchId = created.Organization.Branches.Single().BranchId, DeviceId = Guid.NewGuid(),
                UpdateRolloutId = Guid.NewGuid(), UpdatePackageId = Guid.NewGuid(), Component = "agent",
                InstalledVersion = "1", TargetVersion = "2", Status = UpdateStatusNames.Failed,
                Message = "failed", FirstReportedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var summaries = await client.GetFromJsonAsync<List<OrganizationSummaryDto>>("/api/platform/organizations");
        var summary = Assert.Single(summaries!);
        Assert.Equal(1, summary.RecentErrorCount);
        Assert.Equal(1, summary.ExpiringOwnerInviteCount);
        Assert.Equal(1, summary.RolloutAttentionCount);
    }

    [Fact]
    public async Task GetOrganizationById_ReturnsDetailWithBranchesAndParsedLimits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        var detailResponse = await client.GetAsync($"/api/platform/organizations/{created.Organization.OrganizationId:D}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.Limits.MaxBranches);
        Assert.Equal(60, detail.Limits.MaxDevicesPerBranch);
        Assert.Equal(80, detail.Limits.MaxConcurrentSessions);
        Assert.Equal(20, detail.Limits.MaxStaffUsersPerBranch);
        Assert.Single(detail.Branches);
    }

    [Fact]
    public async Task GetOrganizationById_WithUnknownId_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationOwnerInvites_RotatesPendingInvitesAndReturnsFresh()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);
        var originalInvite = created.OrganizationOwnerInvite;
        var branchId = created.Organization.Branches[0].BranchId;

        var rotateRequest = new CreateOrganizationOwnerInviteRequest(
            BranchId: branchId,
            OwnerUserName: "owner2@demo-club.test",
            OwnerDisplayName: "Replacement Owner",
            Lifetime: TimeSpan.FromDays(14));
        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{created.Organization.OrganizationId:D}/organization-owner-invitations",
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{created.Organization.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(Guid.NewGuid(), null, null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostAccountActivation_WithValidCode_CreatesOwnerStaffWithoutIssuingTokens()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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
        Assert.Equal(created.Organization.OrganizationId, json.GetProperty("organizationId").GetGuid());
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
        Assert.Equal(created.Organization.Branches[0].BranchId, assignment.BranchId);
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        // Rotate to revoke the original invite.
        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{created.Organization.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(created.Organization.Branches[0].BranchId, null, null, null));
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        // Pre-create a staff user with the username we will try to claim.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.StaffUsers.Add(new StaffUserEntity
            {
                StaffUserId = Guid.NewGuid(),
                OrganizationId = created.Organization.OrganizationId,
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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
        Assert.Equal(created.Organization.OrganizationId, audit.OrganizationId);
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
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
    public async Task PostOrganizations_WithIdempotencyKey_StoresAndReplaysOnRetry()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        const string idempotencyKey = "organization-create-attempt-001";
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/platform/organizations")
        {
            Content = JsonContent.Create(BuildCreateOrganizationRequest())
        };
        first.Headers.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.SendAsync(first);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.False(firstResponse.Headers.Contains("Idempotency-Replayed"));

        using var retry = new HttpRequestMessage(HttpMethod.Post, "/api/platform/organizations")
        {
            Content = JsonContent.Create(BuildCreateOrganizationRequest())
        };
        retry.Headers.Add("Idempotency-Key", idempotencyKey);
        var retryResponse = await client.SendAsync(retry);
        var retryBody = await retryResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.True(retryResponse.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal("true", retryResponse.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.NotNull(retryBody);
        Assert.Equal(firstBody.Organization.OrganizationId, retryBody.Organization.OrganizationId);
        Assert.Equal(firstBody.OrganizationOwnerInvite.Code, retryBody.OrganizationOwnerInvite.Code);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
        Assert.Single(await dbContext.PlatformIdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task PostOrganizations_WithIdempotencyKeyReusedForDifferentBody_Returns422()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        const string idempotencyKey = "organization-create-attempt-002";
        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/platform/organizations")
        {
            Content = JsonContent.Create(BuildCreateOrganizationRequest(orgSlug: "club-one"))
        };
        first.Headers.Add("Idempotency-Key", idempotencyKey);
        var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/platform/organizations")
        {
            Content = JsonContent.Create(BuildCreateOrganizationRequest(orgSlug: "club-two"))
        };
        second.Headers.Add("Idempotency-Key", idempotencyKey);
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
    }

    [Fact]
    public async Task PostOrganizations_WithoutIdempotencyKey_KeepsSlugBased409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var first = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.Organizations.ToListAsync());
        Assert.Empty(await dbContext.PlatformIdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task GetOrganizationOwnerInvites_ReturnsAllInvitesForOrganizationWithMaskedCodes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        var rotateResponse = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{created.Organization.OrganizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(created.Organization.Branches[0].BranchId, OwnerUserName: "owner-2@demo-club.test", OwnerDisplayName: "Owner Two", Lifetime: TimeSpan.FromDays(7)));
        var rotated = await rotateResponse.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();
        Assert.NotNull(rotated);

        var listResponse = await client.GetAsync($"/api/platform/organizations/{created.Organization.OrganizationId:D}/organization-owner-invitations");
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
        Assert.Equal(created.Organization.OrganizationId, audit.OrganizationId);
        Assert.Equal(admin.PlatformAdminId, audit.ActorPlatformAdminUserId);
    }

    [Fact]
    public async Task GetOrganizationOwnerInvites_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/organization-owner-invitations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationOwnerInvites_WhenOrganizationMissing_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid():D}/organization-owner-invitations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizations_SeedsOrganizationSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleAsync(s => s.OrganizationId == body!.Organization.OrganizationId);
        Assert.Equal("starter", subscription.PlanCode);
        Assert.Equal(290000, subscription.AmountMinorUnits); // from seeded starter plan
    }

    [Fact]
    public async Task StaffAccessToken_CannotReachPlatformOrganizationEndpoints()
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

        var response = await staffClient.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
