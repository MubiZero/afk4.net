using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfPlanCatalogServiceTests
{
    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CreatePlanRequest BuildCreate(string planCode = "team") =>
        new(
            PlanCode: planCode,
            Name: "Team",
            PriceMinorUnits: 500000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 2,
            MaxDevicesPerBranch: 40,
            MaxConcurrentSessions: 60,
            MaxStaffUsersPerBranch: 15,
            SortOrder: 5);

    [Fact]
    public async Task CreateAsync_PersistsPlanAndReturnsDto()
    {
        await using var db = NewContext();
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z"));
        var service = new EfPlanCatalogService(db, time);

        var result = await service.CreateAsync(BuildCreate(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("team", result.Value!.PlanCode);
        Assert.True(result.Value.IsActive);
        var stored = await db.SubscriptionPlans.SingleAsync();
        Assert.Equal(500000, stored.PriceMinorUnits);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePlanCode_ReturnsConflict()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));
        await service.CreateAsync(BuildCreate(), CancellationToken.None);

        var result = await service.CreateAsync(BuildCreate(), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task CreateAsync_InvalidInterval_ReturnsBadRequest()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));

        var result = await service.CreateAsync(BuildCreate() with { BillingInterval = "weekly" }, CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ChangesFieldsAndBumpsUpdatedAt()
    {
        await using var db = NewContext();
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z"));
        var service = new EfPlanCatalogService(db, time);
        await service.CreateAsync(BuildCreate(), CancellationToken.None);
        time.Now = DateTimeOffset.Parse("2026-06-01T10:00:00Z");

        var result = await service.UpdateAsync("team", new UpdatePlanRequest(
            Name: "Team Plus",
            PriceMinorUnits: 600000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 3,
            MaxDevicesPerBranch: 40,
            MaxConcurrentSessions: 60,
            MaxStaffUsersPerBranch: 15,
            IsActive: false,
            SortOrder: 5), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Team Plus", result.Value!.Name);
        Assert.False(result.Value.IsActive);
        var stored = await db.SubscriptionPlans.SingleAsync();
        Assert.Equal(time.Now, stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPlan_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));

        var result = await service.UpdateAsync("ghost", new UpdatePlanRequest(
            "X", 1, "RUB", BillingIntervalNames.Monthly, null, null, null, null, true, 1), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListAsync_ExcludesInactiveUnlessRequested()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));
        await service.CreateAsync(BuildCreate("a"), CancellationToken.None);
        await service.CreateAsync(BuildCreate("b"), CancellationToken.None);
        await service.UpdateAsync("b", new UpdatePlanRequest(
            "B", 1, "RUB", BillingIntervalNames.Monthly, null, null, null, null, false, 9), CancellationToken.None);

        var active = await service.ListAsync(includeInactive: false, CancellationToken.None);
        var all = await service.ListAsync(includeInactive: true, CancellationToken.None);

        Assert.Single(active);
        Assert.Equal(2, all.Count);
    }
}
