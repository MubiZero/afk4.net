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
