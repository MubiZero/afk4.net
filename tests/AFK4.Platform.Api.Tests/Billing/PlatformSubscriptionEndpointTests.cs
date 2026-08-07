using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformSubscriptionEndpointTests
{
    private static async Task<Guid> CreateOrganizationAsync(PlatformApiFactory factory, HttpClient client)
    {
        var request = new CreateOrganizationRequest(
            OrganizationSlug: "sub-club",
            OrganizationName: "Sub Club",
            BranchSlug: "main",
            BranchName: "Main",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Active,
            Limits: new OrganizationLimitsDto(1, 30, 40, 10),
            OwnerUserName: "owner@sub-club.test",
            OwnerDisplayName: "Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
        var response = await client.PostAsJsonAsync("/api/platform/organizations", request);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        return body!.Organization.OrganizationId;
    }

    [Fact]
    public async Task GetSubscription_ReturnsSeededSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateOrganizationAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{orgId}/subscription");
        var body = await response.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("starter", body!.PlanCode);
    }

    [Fact]
    public async Task PatchSubscription_ChangesPlan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateOrganizationAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{orgId}/subscription",
            new UpdateSubscriptionRequest("scale", null, null, null, null, null, null));
        var body = await response.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("scale", body!.PlanCode);
    }

    [Fact]
    public async Task GetSubscription_UnknownOrganization_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid()}/subscription");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/organizations/{Guid.NewGuid()}/subscription");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{Guid.NewGuid()}/subscription",
            new UpdateSubscriptionRequest("scale", null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBillingStatus_AsOwner_ReturnsStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/billing/status");
        var body = await response.Content.ReadFromJsonAsync<OrganizationBillingStatusDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body!.InArrears);
    }

    [Fact]
    public async Task GetBillingStatus_WithoutViewSubscriptionPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/billing/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // IDOR guard: a staff member of one organization must not be able to read another
    // organization's billing status by swapping the route's organizationId.
    [Fact]
    public async Task GetBillingStatus_ForAnotherOrganization_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/billing/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBillingStatus_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/billing/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
