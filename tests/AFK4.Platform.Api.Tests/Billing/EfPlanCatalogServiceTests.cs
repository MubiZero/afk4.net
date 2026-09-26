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
            CurrencyCode: "TJS",
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

    // Тарифы настраиваются в Platform Control (владелец, 2026-09-26): цена за ПК, бесплатные ПК,
    // предел ПК на клуб и функции — и новые лимиты по желанию уходят клубам на тарифе.
    [Fact]
    public async Task UpdateAsync_SetsPerPcTerms_Features_AndAppliesLimitsToClubsOnRequest()
    {
        await using var db = NewContext();
        var now = DateTimeOffset.Parse("2026-09-26T10:00:00Z");
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(now));
        db.PlatformFeatures.AddRange(
            new PlatformFeatureEntity { FeatureKey = "platform_ads", Name = "Реклама", Description = "", CreatedAtUtc = now },
            new PlatformFeatureEntity { FeatureKey = "tips", Name = "Чаевые", Description = "", EnabledByDefault = true, CreatedAtUtc = now });
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = Guid.NewGuid(), Slug = "club", Name = "Клуб", Status = "active", PlanCode = "team",
            LimitsJson = "{\"MaxDevices\":10}", CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        await service.CreateAsync(BuildCreate() with { MaxDevices = 10, IncludedFeatures = ["platform_ads"] }, CancellationToken.None);

        var created = (await service.GetAsync("team", CancellationToken.None))!;
        Assert.Equal(10, created.MaxDevices);
        Assert.Equal(1, created.Clubs);
        Assert.Equal([("platform_ads", true), ("tips", false)], created.Features!.Select(feature => (feature.FeatureKey, feature.IsIncluded)));

        // Без отметки «применить» лимиты клуба остаются прежними.
        var edit = new UpdatePlanRequest("Team", 500000, "TJS", BillingIntervalNames.Monthly, null, null, null, null, true, 5,
            PricePerDeviceMinorUnits: 800, IncludedDevices: 5, MaxDevices: 8);
        await service.UpdateAsync("team", edit, CancellationToken.None);
        Assert.Equal("{\"MaxDevices\":10}", (await db.Organizations.SingleAsync()).LimitsJson);

        var applied = await service.UpdateAsync("team", edit with { ApplyLimitsToClubs = true, IncludedFeatures = ["tips"] }, CancellationToken.None);
        Assert.Equal(800, applied.Value!.PricePerDeviceMinorUnits);
        Assert.Equal(5, applied.Value.IncludedDevices);
        Assert.Equal([("platform_ads", false), ("tips", true)], applied.Value.Features!.Select(feature => (feature.FeatureKey, feature.IsIncluded)));
        Assert.Equal(8, AFK4.Platform.Api.Platform.Entitlements.OrganizationLimitsJson.Deserialize((await db.Organizations.SingleAsync()).LimitsJson).MaxDevices);

        // Предел не передан — остаётся; снять его — только явно.
        Assert.Equal(8, (await service.UpdateAsync("team", edit with { MaxDevices = null }, CancellationToken.None)).Value!.MaxDevices);
        Assert.Null((await service.UpdateAsync("team", edit with { MaxDevices = null, RemoveMaxDevices = true }, CancellationToken.None)).Value!.MaxDevices);
        Assert.Equal(BillingOperationStatus.BadRequest,
            (await service.UpdateAsync("team", edit with { IncludedFeatures = ["nope"] }, CancellationToken.None)).Status);
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
            CurrencyCode: "TJS",
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
            "X", 1, "TJS", BillingIntervalNames.Monthly, null, null, null, null, true, 1), CancellationToken.None);

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
            "B", 1, "TJS", BillingIntervalNames.Monthly, null, null, null, null, false, 9), CancellationToken.None);

        var active = await service.ListAsync(includeInactive: false, CancellationToken.None);
        var all = await service.ListAsync(includeInactive: true, CancellationToken.None);

        Assert.Single(active);
        Assert.Equal(2, all.Count);
    }
}
