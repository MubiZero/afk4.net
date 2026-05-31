using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
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

    [Fact]
    public async Task StartAsync_SeedsThreeDefaultPlansWhenEmpty()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var provider = BuildProvider(dbName);
        var service = new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var codes = await db.SubscriptionPlans.Select(plan => plan.PlanCode).OrderBy(code => code).ToListAsync();
        Assert.Equal(new[] { TenantPlanCodeNames.Growth, TenantPlanCodeNames.Scale, TenantPlanCodeNames.Starter }, codes);
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
        Assert.Equal(3, await db.SubscriptionPlans.CountAsync());
    }
}
