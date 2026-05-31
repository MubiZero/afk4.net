using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class BillingPlanSeedHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<BillingPlanSeedHostedService> logger) : IHostedService
{
    private static readonly SubscriptionPlanEntity[] DefaultPlans =
    [
        new()
        {
            PlanCode = TenantPlanCodeNames.Starter,
            Name = "Starter",
            PriceMinorUnits = 290000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 1,
            MaxDevicesPerBranch = 30,
            MaxConcurrentSessions = 40,
            MaxStaffUsersPerBranch = 10,
            IsActive = true,
            SortOrder = 1
        },
        new()
        {
            PlanCode = TenantPlanCodeNames.Growth,
            Name = "Growth",
            PriceMinorUnits = 790000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 3,
            MaxDevicesPerBranch = 60,
            MaxConcurrentSessions = 80,
            MaxStaffUsersPerBranch = 20,
            IsActive = true,
            SortOrder = 2
        },
        new()
        {
            PlanCode = TenantPlanCodeNames.Scale,
            Name = "Scale",
            PriceMinorUnits = 1990000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 10,
            MaxDevicesPerBranch = 120,
            MaxConcurrentSessions = 200,
            MaxStaffUsersPerBranch = 50,
            IsActive = true,
            SortOrder = 3
        }
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        if (await dbContext.SubscriptionPlans.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Subscription plan catalog already populated; skipping seed.");
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var template in DefaultPlans)
        {
            dbContext.SubscriptionPlans.Add(new SubscriptionPlanEntity
            {
                PlanCode = template.PlanCode,
                Name = template.Name,
                PriceMinorUnits = template.PriceMinorUnits,
                CurrencyCode = template.CurrencyCode,
                BillingInterval = template.BillingInterval,
                MaxBranches = template.MaxBranches,
                MaxDevicesPerBranch = template.MaxDevicesPerBranch,
                MaxConcurrentSessions = template.MaxConcurrentSessions,
                MaxStaffUsersPerBranch = template.MaxStaffUsersPerBranch,
                IsActive = template.IsActive,
                SortOrder = template.SortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} default subscription plans.", DefaultPlans.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
