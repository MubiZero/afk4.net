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
    // Design spec §6: "The plan seeder rewrites the three known plan codes; custom plans are left
    // alone."
    [Fact]
    public async Task StartAsync_CatalogHasOnlyMonthlyPlans_AddsMissingYearlyPlans()
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
                    PlanCode = OrganizationPlanCodeNames.Starter, Name = "Starter (старое)", PriceMinorUnits = 1,
                    CurrencyCode = "RUB", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 1,
                    MaxDevicesPerBranch = 1, MaxConcurrentSessions = 1, MaxStaffUsersPerBranch = 1,
                    IsActive = true, SortOrder = 1, CreatedAtUtc = now, UpdatedAtUtc = now
                },
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.Growth, Name = "Growth (старое)", PriceMinorUnits = 1,
                    CurrencyCode = "RUB", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 1,
                    MaxDevicesPerBranch = 1, MaxConcurrentSessions = 1, MaxStaffUsersPerBranch = 1,
                    IsActive = true, SortOrder = 2, CreatedAtUtc = now, UpdatedAtUtc = now
                },
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.Scale, Name = "Scale (старое)", PriceMinorUnits = 1,
                    CurrencyCode = "RUB", BillingInterval = BillingIntervalNames.Monthly, MaxBranches = 1,
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

        // The three known monthly codes are rewritten to the canonical TJS pricing, not left at
        // their stale RUB values.
        Assert.Equal("TJS", plans[OrganizationPlanCodeNames.Starter].CurrencyCode);
        Assert.Equal(290000, plans[OrganizationPlanCodeNames.Starter].PriceMinorUnits);
        Assert.Equal("TJS", plans[OrganizationPlanCodeNames.Growth].CurrencyCode);
        Assert.Equal("TJS", plans[OrganizationPlanCodeNames.Scale].CurrencyCode);

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
