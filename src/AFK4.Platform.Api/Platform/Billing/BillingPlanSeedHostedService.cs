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

        // Adds the known plan codes that are missing and never touches a code that already exists.
        // The platform panel is authoritative for price/name/limits/active/sort order once a plan
        // row exists (EfPlanCatalogService.UpdateAsync lets staff edit it) — a seeder that rewrote
        // those fields on every restart would silently revert a deliberate panel edit on the next
        // deploy. It still never bails out just because the catalog is non-empty: a production-like
        // database that already has the pre-yearly monthly plans must still gain the yearly codes on
        // the next deploy; an AnyAsync() early return would leave them missing forever (design spec
        // §6: the seeder only adds missing known codes; existing rows, including custom plans, are
        // left alone).
        var existingCodes = await dbContext.SubscriptionPlans
            .Select(plan => plan.PlanCode)
            .ToHashSetAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var template in DefaultPlans)
        {
            if (existingCodes.Contains(template.PlanCode))
            {
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

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Subscription plan catalog seed: added {Added} missing known plans.", added);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
