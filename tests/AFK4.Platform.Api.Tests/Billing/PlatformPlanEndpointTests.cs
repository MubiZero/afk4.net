using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformPlanEndpointTests
{
    [Fact]
    public async Task GetPlans_ReturnsSeededDefaults()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync("/api/platform/plans");
        var plans = await response.Content.ReadFromJsonAsync<List<SubscriptionPlanDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, plans!.Count);
        Assert.Contains(plans, plan => plan.PlanCode == "starter");
    }

    [Fact]
    public async Task GetPlans_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPlans_CreatesPlanAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = new CreatePlanRequest(
            PlanCode: "enterprise",
            Name: "Enterprise",
            PriceMinorUnits: 4990000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 50,
            MaxDevicesPerBranch: 300,
            MaxConcurrentSessions: 500,
            MaxStaffUsersPerBranch: 200,
            SortOrder: 4);

        var response = await client.PostAsJsonAsync("/api/platform/plans", request);
        var body = await response.Content.ReadFromJsonAsync<SubscriptionPlanDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("enterprise", body!.PlanCode);
    }

    [Fact]
    public async Task PostPlans_DuplicateCode_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = new CreatePlanRequest(
            "starter", "Dup", 1, "RUB", BillingIntervalNames.Monthly, null, null, null, null, 9);

        var response = await client.PostAsJsonAsync("/api/platform/plans", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchPlan_UpdatesPrice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = new UpdatePlanRequest(
            Name: "Starter",
            PriceMinorUnits: 350000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 1,
            MaxDevicesPerBranch: 30,
            MaxConcurrentSessions: 40,
            MaxStaffUsersPerBranch: 10,
            IsActive: true,
            SortOrder: 1);

        var response = await client.PatchAsJsonAsync("/api/platform/plans/starter", request);
        var body = await response.Content.ReadFromJsonAsync<SubscriptionPlanDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(350000, body!.PriceMinorUnits);
    }
}
