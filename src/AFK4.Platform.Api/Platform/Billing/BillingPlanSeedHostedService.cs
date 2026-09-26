using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Features;
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
        // Спека тарифов клуба, §2: бесплатно до десяти ПК с рекламой платформы, дальше — за ПК.
        new()
        {
            PlanCode = OrganizationPlanCodeNames.Free,
            Name = "Бесплатный",
            PriceMinorUnits = 0,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            // Только число ПК на весь клуб: залы и сотрудники не ограничены (владелец, 2026-09-26).
            MaxBranches = null,
            MaxDevicesPerBranch = null,
            MaxConcurrentSessions = null,
            MaxStaffUsersPerBranch = null,
            MaxDevices = ClubPlanLimits.FreeDevices,
            IsActive = true,
            SortOrder = 0
        },
        new()
        {
            PlanCode = OrganizationPlanCodeNames.PerPc,
            Name = "За ПК",
            PriceMinorUnits = 0,
            PricePerDeviceMinorUnits = ClubPlanLimits.PricePerDeviceMinorUnits,
            IncludedDevices = ClubPlanLimits.FreeDevices,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = null,
            MaxDevicesPerBranch = null,
            MaxConcurrentSessions = null,
            MaxStaffUsersPerBranch = null,
            IsActive = true,
            SortOrder = 0
        },
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
            // Прежняя сетка снята с продажи (спека тарифов клуба, §2): 2 900 были рублями без пересчёта.
            IsActive = false,
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
            // Прежняя сетка снята с продажи (спека тарифов клуба, §2): 2 900 были рублями без пересчёта.
            IsActive = false,
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
            // Прежняя сетка снята с продажи (спека тарифов клуба, §2): 2 900 были рублями без пересчёта.
            IsActive = false,
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
            // Прежняя сетка снята с продажи (спека тарифов клуба, §2): 2 900 были рублями без пересчёта.
            IsActive = false,
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
            // Прежняя сетка снята с продажи (спека тарифов клуба, §2): 2 900 были рублями без пересчёта.
            IsActive = false,
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
            // Прежняя сетка снята с продажи (спека тарифов клуба, §2): 2 900 были рублями без пересчёта.
            IsActive = false,
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
                MaxDevices = template.MaxDevices,
                PricePerDeviceMinorUnits = template.PricePerDeviceMinorUnits,
                IncludedDevices = template.IncludedDevices,
                IsActive = template.IsActive,
                SortOrder = template.SortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            added++;
        }

        // Реклама платформы — у бесплатного тарифа и только у него. Строки нет — решает значение
        // фичи по умолчанию; здесь оно записано явно, чтобы клуб на бесплатном её получил.
        foreach (var (planCode, included) in new[] { (OrganizationPlanCodeNames.Free, true), (OrganizationPlanCodeNames.PerPc, false) })
        {
            var known = await dbContext.PlanFeatures.AnyAsync(
                feature => feature.PlanCode == planCode && feature.FeatureKey == PlatformFeatureNames.PlatformAds, cancellationToken);
            if (known) continue;
            dbContext.PlanFeatures.Add(new PlanFeatureEntity
            {
                PlanFeatureId = Guid.NewGuid(), PlanCode = planCode, FeatureKey = PlatformFeatureNames.PlatformAds, IsIncluded = included
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
