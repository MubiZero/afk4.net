using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class BillingPlanSeedHostedServiceTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static BillingPlanSeedHostedService NewService(PlatformDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();
        return new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);
    }

    [Fact]
    public async Task StartAsync_SeedsMonthlyAndYearlyDefaultPlansWhenEmpty()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var provider = BuildProvider(dbName);
        var service = new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var codes = await db.SubscriptionPlans.Select(plan => plan.PlanCode).OrderBy(code => code).ToListAsync();
        Assert.Equal(
            new[]
            {
                OrganizationPlanCodeNames.Growth,
                "growth_yearly",
                OrganizationPlanCodeNames.Scale,
                "scale_yearly",
                OrganizationPlanCodeNames.Starter,
                "starter_yearly"
            },
            codes);
    }

    [Fact]
    public async Task StartAsync_DoesNotDuplicateWhenPlansExist()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var provider = BuildProvider(dbName);
        var service = new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(6, await db.SubscriptionPlans.CountAsync());
    }

    // Regression for a review finding: StartAsync used to bail out on AnyAsync() — a staging/prod
    // database that already had the three pre-existing monthly plans (from before yearly plans were
    // introduced) would never gain the three yearly plans, since the catalog was never truly empty.
    // It also used to rewrite the three known monthly codes on every restart, which silently reverted
    // any price/name/limit the platform panel had edited. The panel is authoritative: the seeder must
    // only add codes that are missing and must never touch a row that already exists (design spec §6).
    [Fact]
    public async Task StartAsync_CatalogHasOnlyMonthlyPlans_AddsMissingYearlyPlansAndPreservesExistingEdits()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var provider = BuildProvider(dbName);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.Starter, Name = "Starter (изменён из панели)",
                    PriceMinorUnits = 350000,
                    CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 1,
                    MaxDevicesPerBranch = 1, MaxConcurrentSessions = 1, MaxStaffUsersPerBranch = 1,
                    IsActive = false, SortOrder = 1, CreatedAtUtc = now, UpdatedAtUtc = now
                },
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.Growth, Name = "Growth (изменён из панели)",
                    PriceMinorUnits = 850000,
                    CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 1,
                    MaxDevicesPerBranch = 1, MaxConcurrentSessions = 1, MaxStaffUsersPerBranch = 1,
                    IsActive = true, SortOrder = 2, CreatedAtUtc = now, UpdatedAtUtc = now
                },
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.Scale, Name = "Scale (изменён из панели)",
                    PriceMinorUnits = 2100000,
                    CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 1,
                    MaxDevicesPerBranch = 1, MaxConcurrentSessions = 1, MaxStaffUsersPerBranch = 1,
                    IsActive = true, SortOrder = 3, CreatedAtUtc = now, UpdatedAtUtc = now
                },
                new SubscriptionPlanEntity
                {
                    PlanCode = "custom_negotiated", Name = "Custom", PriceMinorUnits = 999999,
                    CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 99,
                    MaxDevicesPerBranch = 99, MaxConcurrentSessions = 99, MaxStaffUsersPerBranch = 99,
                    IsActive = true, SortOrder = 99, CreatedAtUtc = now, UpdatedAtUtc = now
                });
            await db.SaveChangesAsync();
        }

        var service = new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);
        await service.StartAsync(CancellationToken.None);

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var plans = await verifyDb.SubscriptionPlans.ToDictionaryAsync(plan => plan.PlanCode);

        // The three yearly codes were missing and must now exist.
        Assert.True(plans.ContainsKey("starter_yearly"));
        Assert.True(plans.ContainsKey("growth_yearly"));
        Assert.True(plans.ContainsKey("scale_yearly"));

        // The three known monthly codes already existed — the panel's edits (price, name,
        // IsActive) must survive the seed untouched, not get reverted to the hardcoded defaults.
        Assert.Equal("Starter (изменён из панели)", plans[OrganizationPlanCodeNames.Starter].Name);
        Assert.Equal(350000, plans[OrganizationPlanCodeNames.Starter].PriceMinorUnits);
        Assert.False(plans[OrganizationPlanCodeNames.Starter].IsActive);
        Assert.Equal("Growth (изменён из панели)", plans[OrganizationPlanCodeNames.Growth].Name);
        Assert.Equal(850000, plans[OrganizationPlanCodeNames.Growth].PriceMinorUnits);
        Assert.Equal("Scale (изменён из панели)", plans[OrganizationPlanCodeNames.Scale].Name);
        Assert.Equal(2100000, plans[OrganizationPlanCodeNames.Scale].PriceMinorUnits);

        // A custom, hand-negotiated plan code is not one of the three known codes and must survive
        // untouched.
        Assert.True(plans.ContainsKey("custom_negotiated"));
        Assert.Equal(999999, plans["custom_negotiated"].PriceMinorUnits);
        Assert.Equal("TJS", plans["custom_negotiated"].CurrencyCode);

        Assert.Equal(7, plans.Count);
    }

    [Fact]
    public async Task StartAsync_SeedsMonthlyAndYearlyPlansInSomoni()
    {
        await using var db = NewContext();
        var service = NewService(db);

        await service.StartAsync(CancellationToken.None);

        var plans = await db.SubscriptionPlans.ToListAsync();
        Assert.All(plans, plan => Assert.Equal("TJS", plan.CurrencyCode));

        var starter = plans.Single(plan => plan.PlanCode == "starter");
        Assert.Equal(290000, starter.PriceMinorUnits);
        Assert.Equal(BillingIntervalNames.Monthly, starter.BillingInterval);

        var starterYearly = plans.Single(plan => plan.PlanCode == "starter_yearly");
        Assert.Equal(2900000, starterYearly.PriceMinorUnits); // ten months, two free
        Assert.Equal(BillingIntervalNames.Yearly, starterYearly.BillingInterval);
        Assert.Equal(starter.MaxBranches, starterYearly.MaxBranches);
    }
}
