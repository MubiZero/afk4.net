using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchDynamicsEndpointTests
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
            OwnerUserName: null,
            OwnerDisplayName: null,
            OrganizationOwnerInviteLifetime: null);
    }

    private static async Task<(Guid OrganizationId, Guid BranchId)> CreateOrganizationAsync(
        HttpClient client,
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch")
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            BuildCreateOrganizationRequest(orgSlug, branchSlug));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return (body.Organization.OrganizationId, body.Organization.Branches[0].BranchId);
    }

    private static BranchDailySnapshotEntity BuildSnapshot(
        Guid organizationId,
        Guid branchId,
        DateOnly date,
        int sessionCount = 1,
        long revenueMinorUnits = 10_000,
        int shiftOpenedCount = 1,
        bool? agentAlive = true) => new()
    {
        BranchDailySnapshotId = Guid.NewGuid(),
        OrganizationId = organizationId,
        BranchId = branchId,
        SnapshotDate = date,
        SessionCount = sessionCount,
        RevenueMinorUnits = revenueMinorUnits,
        CurrencyCode = "TJS",
        ShiftOpenedCount = shiftOpenedCount,
        AgentAlive = agentAlive,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Get_ReturnsSnapshotDays_NewestLast()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        var day1 = Today.AddDays(-3);
        var day2 = Today.AddDays(-2);
        var day3 = Today.AddDays(-1);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, day3, sessionCount: 3, revenueMinorUnits: 30_000));
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, day1, sessionCount: 1, revenueMinorUnits: 10_000));
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, day2, sessionCount: 2, revenueMinorUnits: 20_000));
            await dbContext.SaveChangesAsync();
        }

        var dynamics = await client.GetFromJsonAsync<BranchDynamicsDto>(
            $"/api/platform/organizations/{organizationId}/branches/{branchId}/dynamics?days=30");

        Assert.NotNull(dynamics);
        Assert.Equal(3, dynamics.Days.Count);
        Assert.Equal([day1, day2, day3], dynamics.Days.Select(day => day.Date).ToArray());
        Assert.Equal(6, dynamics.TotalSessionCount);
        Assert.Equal(60_000, dynamics.TotalRevenue.MinorUnits);
    }

    [Fact]
    public async Task Get_DoesNotInventZeroDaysForMissingSnapshots()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, Today.AddDays(-1)));
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, Today.AddDays(-2)));
            await dbContext.SaveChangesAsync();
        }

        var dynamics = await client.GetFromJsonAsync<BranchDynamicsDto>(
            $"/api/platform/organizations/{organizationId}/branches/{branchId}/dynamics?days=30");

        Assert.NotNull(dynamics);
        Assert.Equal(2, dynamics.Days.Count);
        Assert.Equal(28, dynamics.MissingDayCount);
    }

    [Fact]
    public async Task Get_CountsUnknownAgentSeparatelyFromDead()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, Today.AddDays(-1), agentAlive: true));
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, Today.AddDays(-2), agentAlive: false));
            dbContext.BranchDailySnapshots.Add(BuildSnapshot(organizationId, branchId, Today.AddDays(-3), agentAlive: null));
            await dbContext.SaveChangesAsync();
        }

        var dynamics = await client.GetFromJsonAsync<BranchDynamicsDto>(
            $"/api/platform/organizations/{organizationId}/branches/{branchId}/dynamics?days=30");

        Assert.NotNull(dynamics);
        Assert.Equal(1, dynamics.DaysWithoutAgent);
        Assert.Equal(1, dynamics.DaysWithUnknownAgent);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenBranchBelongsToAnotherOrganization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (_, branchId) = await CreateOrganizationAsync(client, "org-a", "branch-a");
        var (otherOrganizationId, _) = await CreateOrganizationAsync(client, "org-b", "branch-b");

        var response = await client.GetAsync(
            $"/api/platform/organizations/{otherOrganizationId}/branches/{branchId}/dynamics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsForbidden_WithoutViewOrganizationsPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync(
            $"/api/platform/organizations/{Guid.NewGuid()}/branches/{Guid.NewGuid()}/dynamics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(body));
    }

    [Fact]
    public async Task Get_ClampsDays_ToTheSupportedRange()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        var wideWindow = await client.GetFromJsonAsync<BranchDynamicsDto>(
            $"/api/platform/organizations/{organizationId}/branches/{branchId}/dynamics?days=1000");
        var zeroWindow = await client.GetFromJsonAsync<BranchDynamicsDto>(
            $"/api/platform/organizations/{organizationId}/branches/{branchId}/dynamics?days=0");

        Assert.NotNull(wideWindow);
        Assert.NotNull(zeroWindow);
        Assert.Equal(90, wideWindow.ToDate.DayNumber - wideWindow.FromDate.DayNumber + 1);
        Assert.True(zeroWindow.ToDate.DayNumber - zeroWindow.FromDate.DayNumber + 1 >= 7);
    }
}
