using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformSubscriptionEndpointTests
{
    private static async Task<Guid> CreateTenantAsync(PlatformApiFactory factory, HttpClient client)
    {
        var request = new CreateTenantRequest(
            OrganizationSlug: "sub-club",
            OrganizationName: "Sub Club",
            BranchSlug: "main",
            BranchName: "Main",
            BranchCity: "Dushanbe",
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Active,
            Limits: new TenantLimitsDto(1, 30, 40, 10),
            OwnerUserName: "owner@sub-club.test",
            OwnerDisplayName: "Owner",
            OwnerInviteLifetime: TimeSpan.FromDays(7));
        var response = await client.PostAsJsonAsync("/api/platform/tenants", request);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        return body!.Tenant.OrganizationId;
    }

    [Fact]
    public async Task GetSubscription_ReturnsSeededSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateTenantAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{orgId}/subscription");
        var body = await response.Content.ReadFromJsonAsync<TenantSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("starter", body!.PlanCode);
    }

    [Fact]
    public async Task PatchSubscription_ChangesPlan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateTenantAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{orgId}/subscription",
            new UpdateSubscriptionRequest("scale", null, null, null));
        var body = await response.Content.ReadFromJsonAsync<TenantSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("scale", body!.PlanCode);
    }

    [Fact]
    public async Task GetSubscription_UnknownTenant_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid()}/subscription");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid()}/subscription");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{Guid.NewGuid()}/subscription",
            new UpdateSubscriptionRequest("scale", null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
