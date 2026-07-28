using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformOrganizationLifecycleEndpointTests
{
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

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return body.Organization.OrganizationId;
    }

    [Fact]
    public async Task PatchStatus_SuspendsOrganizationWithReasonAndPersistsAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, "Unpaid invoice"));
        var body = await response.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(OrganizationStatusNames.Suspended, body.Status);
        Assert.Equal("Unpaid invoice", body.StatusReason);
        Assert.NotNull(body.StatusChangedAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal(OrganizationStatusNames.Suspended, organization.Status);
        Assert.Equal("Unpaid invoice", organization.StatusReason);

        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "tenancy.organization.status.update" && record.Outcome == "Succeeded")
            .SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Contains("\"NewStatus\":\"suspended\"", audit.DetailsJson);
    }

    [Fact]
    public async Task PatchStatus_ReactivatesOrganizationWithoutReason()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var suspend = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, "Pause"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var reactivate = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Active, ""));
        var body = await reactivate.Content.ReadFromJsonAsync<OrganizationDetailDto>();

        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(OrganizationStatusNames.Active, body.Status);
        Assert.Null(body.StatusReason);
    }

    [Fact]
    public async Task PatchStatus_WithoutReason_WhenSuspending_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_WithUnknownStatus_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest("frozen", "any"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_WithUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, "Why not"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, "Why"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_DeletionPending_RecordsAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.DeletionPending, "Owner requested deletion"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await dbContext.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        Assert.Equal(OrganizationStatusNames.DeletionPending, organization.Status);
        Assert.Equal("Owner requested deletion", organization.StatusReason);
    }

    [Fact]
    public async Task PatchLimits_UpdatesAndReturnsParsedLimits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/limits",
            new UpdateOrganizationLimitsRequest(
                new OrganizationLimitsDto(MaxBranches: 7, MaxDevicesPerBranch: 200, MaxConcurrentSessions: null, MaxStaffUsersPerBranch: 50)));
        var body = await response.Content.ReadFromJsonAsync<OrganizationDetailDto>();

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

        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/limits",
            new UpdateOrganizationLimitsRequest(
                new OrganizationLimitsDto(MaxBranches: -1, MaxDevicesPerBranch: null, MaxConcurrentSessions: null, MaxStaffUsersPerBranch: null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchLimits_WithUnknownOrganization_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid():D}/limits",
            new UpdateOrganizationLimitsRequest(new OrganizationLimitsDto(1, 2, 3, 4)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostOrganizationOwnerInviteAccept_WhenOrganizationSuspended_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(created);

        var suspend = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{created.Organization.OrganizationId:D}/status",
            new UpdateOrganizationStatusRequest(OrganizationStatusNames.Suspended, "Holding onboarding"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        using var publicClient = factory.CreateClient();
        var response = await publicClient.PostAsJsonAsync(
            "/api/account-activation/organization-owner",
            new AcceptOrganizationOwnerInviteRequest(
                Code: created.OrganizationOwnerInvite.Code,
                UserName: "demo.owner",
                DisplayName: "Demo Owner",
                Password: "Passw0rd!Real"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
