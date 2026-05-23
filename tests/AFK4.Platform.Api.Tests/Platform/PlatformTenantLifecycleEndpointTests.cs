using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformTenantLifecycleEndpointTests
{
    private static CreateTenantRequest BuildCreateTenantRequest(
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch")
    {
        return new CreateTenantRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Demo Club",
            BranchSlug: branchSlug,
            BranchName: "Demo Branch",
            BranchCity: "Dushanbe",
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new TenantLimitsDto(3, 60, 80, 20),
            OwnerUserName: "owner@demo-club.test",
            OwnerDisplayName: "Demo Owner",
            OwnerInviteLifetime: TimeSpan.FromDays(7));
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(body);
        return body.Tenant.OrganizationId;
    }

    [Fact]
    public async Task PatchStatus_SuspendsTenantWithReasonAndPersistsAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, "Unpaid invoice"));
        var body = await response.Content.ReadFromJsonAsync<TenantDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TenantStatusNames.Suspended, body.Status);
        Assert.Equal("Unpaid invoice", body.StatusReason);
        Assert.NotNull(body.StatusChangedAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal(TenantStatusNames.Suspended, organization.Status);
        Assert.Equal("Unpaid invoice", organization.StatusReason);

        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "tenancy.tenant.status.update" && record.Outcome == "Succeeded")
            .SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Contains("\"NewStatus\":\"suspended\"", audit.DetailsJson);
    }

    [Fact]
    public async Task PatchStatus_ReactivatesTenantWithoutReason()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var suspend = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, "Pause"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var reactivate = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Active, ""));
        var body = await reactivate.Content.ReadFromJsonAsync<TenantDetailDto>();

        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TenantStatusNames.Active, body.Status);
        Assert.Null(body.StatusReason);
    }

    [Fact]
    public async Task PatchStatus_WithoutReason_WhenSuspending_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_WithUnknownStatus_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest("frozen", "any"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_WithUnknownTenant_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid():D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, "Why not"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid():D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, "Why"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_DeletionPending_RecordsAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.DeletionPending, "Owner requested deletion"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal(TenantStatusNames.DeletionPending, organization.Status);
        Assert.Equal("Owner requested deletion", organization.StatusReason);
    }

    [Fact]
    public async Task PatchPlan_UpdatesPlanAndSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/plan",
            new UpdateTenantPlanRequest(TenantPlanCodeNames.Scale, SubscriptionStatusNames.Active));
        var body = await response.Content.ReadFromJsonAsync<TenantDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TenantPlanCodeNames.Scale, body.PlanCode);
        Assert.Equal(SubscriptionStatusNames.Active, body.SubscriptionStatus);
    }

    [Fact]
    public async Task PatchPlan_WithUnknownSubscriptionStatus_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/plan",
            new UpdateTenantPlanRequest(TenantPlanCodeNames.Growth, "ghost"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchPlan_WithSupportRoleOnly_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateTenantAsync(client);

        using var supportClient = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory,
            supportClient,
            userName: "support@platform.test",
            roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await supportClient.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/plan",
            new UpdateTenantPlanRequest(TenantPlanCodeNames.Scale, SubscriptionStatusNames.Active));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal(TenantPlanCodeNames.Starter, organization.PlanCode);
    }

    [Fact]
    public async Task PatchLimits_UpdatesAndReturnsParsedLimits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/limits",
            new UpdateTenantLimitsRequest(
                new TenantLimitsDto(MaxBranches: 7, MaxDevicesPerBranch: 200, MaxConcurrentSessions: null, MaxStaffUsersPerBranch: 50)));
        var body = await response.Content.ReadFromJsonAsync<TenantDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(7, body.Limits.MaxBranches);
        Assert.Equal(200, body.Limits.MaxDevicesPerBranch);
        Assert.Null(body.Limits.MaxConcurrentSessions);
        Assert.Equal(50, body.Limits.MaxStaffUsersPerBranch);
    }

    [Fact]
    public async Task PatchLimits_WithNegativeValue_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateTenantAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/limits",
            new UpdateTenantLimitsRequest(
                new TenantLimitsDto(MaxBranches: -1, MaxDevicesPerBranch: null, MaxConcurrentSessions: null, MaxStaffUsersPerBranch: null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchLimits_WithUnknownTenant_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid():D}/limits",
            new UpdateTenantLimitsRequest(new TenantLimitsDto(1, 2, 3, 4)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOwnerInviteAccept_WhenTenantSuspended_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.NotNull(created);

        var suspend = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{created.Tenant.OrganizationId:D}/status",
            new UpdateTenantStatusRequest(TenantStatusNames.Suspended, "Holding onboarding"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(
                Code: created.OwnerInvite.Code,
                UserName: "demo.owner",
                DisplayName: "Demo Owner",
                Password: "Passw0rd!Real"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
