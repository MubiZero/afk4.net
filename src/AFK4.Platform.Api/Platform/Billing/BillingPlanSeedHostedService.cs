using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
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
            PlanCode = OrganizationPlanCodeNames.Starter,
            Name = "Starter",
            PriceMinorUnits = 290000,
            CurrencyCode = "TJS",
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
            PlanCode = OrganizationPlanCodeNames.Growth,
            Name = "Growth",
            PriceMinorUnits = 790000,
            CurrencyCode = "TJS",
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
            PlanCode = OrganizationPlanCodeNames.Scale,
            Name = "Scale",
            PriceMinorUnits = 1990000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 10,
            MaxDevicesPerBranch = 120,
            MaxConcurrentSessions = 200,
            MaxStaffUsersPerBranch = 50,
            IsActive = true,
            SortOrder = 3
        },
        new()
        {
            PlanCode = "starter_yearly",
            Name = "Starter, год",
            PriceMinorUnits = 2900000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Yearly,
            MaxBranches = 1,
            MaxDevicesPerBranch = 30,
            MaxConcurrentSessions = 40,
            MaxStaffUsersPerBranch = 10,
            IsActive = true,
            SortOrder = 4
        },
        new()
        {
            PlanCode = "growth_yearly",
            Name = "Growth, год",
            PriceMinorUnits = 7900000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Yearly,
            MaxBranches = 3,
            MaxDevicesPerBranch = 60,
            MaxConcurrentSessions = 80,
            MaxStaffUsersPerBranch = 20,
            IsActive = true,
            SortOrder = 5
        },
        new()
        {
            PlanCode = "scale_yearly",
            Name = "Scale, год",
            PriceMinorUnits = 19900000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Yearly,
            MaxBranches = 10,
            MaxDevicesPerBranch = 120,
            MaxConcurrentSessions = 200,
            MaxStaffUsersPerBranch = 50,
            IsActive = true,
            SortOrder = 6
        }
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Rewrites the three known monthly codes and adds the three known yearly codes when
        // missing — it never bails out just because the catalog is non-empty. A production-like
        // database that already has the pre-TJS monthly plans must still gain the yearly plans on
        // the next deploy; an AnyAsync() early return would leave them missing forever (design spec
        // §6: "The plan seeder rewrites the three known plan codes; custom plans are left alone.").
        var existingByCode = await dbContext.SubscriptionPlans
            .ToDictionaryAsync(plan => plan.PlanCode, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var added = 0;
        var updated = 0;
        foreach (var template in DefaultPlans)
        {
            if (existingByCode.TryGetValue(template.PlanCode, out var existing))
            {
                existing.Name = template.Name;
                existing.PriceMinorUnits = template.PriceMinorUnits;
                existing.CurrencyCode = template.CurrencyCode;
                existing.BillingInterval = template.BillingInterval;
                existing.MaxBranches = template.MaxBranches;
                existing.MaxDevicesPerBranch = template.MaxDevicesPerBranch;
                existing.MaxConcurrentSessions = template.MaxConcurrentSessions;
                existing.MaxStaffUsersPerBranch = template.MaxStaffUsersPerBranch;
                existing.IsActive = template.IsActive;
                existing.SortOrder = template.SortOrder;
                existing.UpdatedAtUtc = now;
                updated++;
                continue;
            }

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
            added++;
        }

        if (added > 0 || updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Subscription plan catalog seed: added {Added}, updated {Updated} known plans.", added, updated);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
