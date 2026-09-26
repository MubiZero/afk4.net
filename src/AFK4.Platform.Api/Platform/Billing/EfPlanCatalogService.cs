using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfPlanCatalogService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlanCatalogService
{
    private const int MaxPlanCodeLength = 64;
    private const int MaxNameLength = 160;

    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.Ordinal)
    {
        BillingIntervalNames.Monthly,
        BillingIntervalNames.Yearly
    };

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SubscriptionPlans.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(plan => plan.IsActive);
        }

        var plans = await query
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.PlanCode)
            .ToListAsync(cancellationToken);
        return await DescribeAsync(plans, cancellationToken);
    }

    public async Task<SubscriptionPlanDto?> GetAsync(string planCode, CancellationToken cancellationToken)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == normalized, cancellationToken);
        return plan is null ? null : (await DescribeAsync([plan], cancellationToken))[0];
    }

    public async Task<BillingOperationResult<SubscriptionPlanDto>> CreateAsync(
        CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var planCode = (request.PlanCode ?? string.Empty).Trim();
        var validationError = ValidateCommon(planCode, request.Name, request.CurrencyCode, request.BillingInterval, request.PriceMinorUnits)
            ?? ValidatePerDevice(request.PricePerDeviceMinorUnits, request.IncludedDevices, request.MaxDevices)
            ?? await ValidateFeaturesAsync(request.IncludedFeatures, cancellationToken);
        if (validationError is not null)
        {
            return BillingOperationResult<SubscriptionPlanDto>.BadRequest(validationError);
        }

        var exists = await dbContext.SubscriptionPlans.AnyAsync(plan => plan.PlanCode == planCode, cancellationToken);
        if (exists)
        {
            return BillingOperationResult<SubscriptionPlanDto>.Conflict($"Plan '{planCode}' already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var entity = new SubscriptionPlanEntity
        {
            PlanCode = planCode,
            Name = request.Name.Trim(),
            PriceMinorUnits = request.PriceMinorUnits,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            BillingInterval = request.BillingInterval.Trim(),
            MaxBranches = request.MaxBranches,
            MaxDevicesPerBranch = request.MaxDevicesPerBranch,
            MaxConcurrentSessions = request.MaxConcurrentSessions,
            MaxStaffUsersPerBranch = request.MaxStaffUsersPerBranch,
            PricePerDeviceMinorUnits = request.PricePerDeviceMinorUnits,
            IncludedDevices = request.IncludedDevices,
            MaxDevices = request.MaxDevices,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.SubscriptionPlans.Add(entity);
        await SetFeaturesAsync(planCode, request.IncludedFeatures, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<SubscriptionPlanDto>.Success((await DescribeAsync([entity], cancellationToken))[0]);
    }

    public async Task<BillingOperationResult<SubscriptionPlanDto>> UpdateAsync(
        string planCode,
        UpdatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var validationError = ValidateCommon(normalized, request.Name, request.CurrencyCode, request.BillingInterval, request.PriceMinorUnits)
            ?? ValidatePerDevice(request.PricePerDeviceMinorUnits ?? 0, request.IncludedDevices ?? 0, request.MaxDevices)
            ?? await ValidateFeaturesAsync(request.IncludedFeatures, cancellationToken);
        if (validationError is not null)
        {
            return BillingOperationResult<SubscriptionPlanDto>.BadRequest(validationError);
        }

        var entity = await dbContext.SubscriptionPlans
            .SingleOrDefaultAsync(plan => plan.PlanCode == normalized, cancellationToken);
        if (entity is null)
        {
            return BillingOperationResult<SubscriptionPlanDto>.NotFound($"Plan '{normalized}' was not found.");
        }

        entity.Name = request.Name.Trim();
        entity.PriceMinorUnits = request.PriceMinorUnits;
        entity.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        entity.BillingInterval = request.BillingInterval.Trim();
        entity.MaxBranches = request.MaxBranches;
        entity.MaxDevicesPerBranch = request.MaxDevicesPerBranch;
        entity.MaxConcurrentSessions = request.MaxConcurrentSessions;
        entity.MaxStaffUsersPerBranch = request.MaxStaffUsersPerBranch;
        entity.PricePerDeviceMinorUnits = request.PricePerDeviceMinorUnits ?? entity.PricePerDeviceMinorUnits;
        entity.IncludedDevices = request.IncludedDevices ?? entity.IncludedDevices;
        entity.MaxDevices = request.RemoveMaxDevices ? null : request.MaxDevices ?? entity.MaxDevices;
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await SetFeaturesAsync(entity.PlanCode, request.IncludedFeatures, cancellationToken);

        // Лимиты клуб получает при смене тарифа, поэтому правка тарифа их не трогает. Платформа
        // просит явно — клубы на тарифе получают новые, и свои лимиты клуба тоже заменяются.
        if (request.ApplyLimitsToClubs)
        {
            var limitsJson = System.Text.Json.JsonSerializer.Serialize(ClubPlans.LimitsOf(entity));
            var clubs = await dbContext.Organizations.Where(organization => organization.PlanCode == entity.PlanCode).ToListAsync(cancellationToken);
            foreach (var club in clubs)
            {
                club.LimitsJson = limitsJson;
                club.UpdatedAtUtc = entity.UpdatedAtUtc;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<SubscriptionPlanDto>.Success((await DescribeAsync([entity], cancellationToken))[0]);
    }

    private static string? ValidateCommon(string planCode, string? name, string? currencyCode, string? interval, long price)
    {
        if (string.IsNullOrWhiteSpace(planCode) || planCode.Length > MaxPlanCodeLength)
        {
            return $"PlanCode is required and must be {MaxPlanCodeLength} characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return $"Name is required and must be {MaxNameLength} characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
        {
            return "CurrencyCode must be a 3-letter code.";
        }

        if (string.IsNullOrWhiteSpace(interval) || !AllowedIntervals.Contains(interval.Trim()))
        {
            return $"BillingInterval must be one of: {string.Join(", ", AllowedIntervals)}.";
        }

        if (price < 0)
        {
            return "PriceMinorUnits must be non-negative.";
        }

        return null;
    }

    private static string? ValidatePerDevice(long pricePerDevice, int includedDevices, int? maxDevices)
    {
        if (pricePerDevice < 0) return "PricePerDeviceMinorUnits must be non-negative.";
        if (includedDevices < 0) return "IncludedDevices must be non-negative.";
        if (maxDevices is < 0) return "MaxDevices must be non-negative.";
        return null;
    }

    private async Task<string?> ValidateFeaturesAsync(IReadOnlyList<string>? features, CancellationToken cancellationToken)
    {
        if (features is null || features.Count == 0) return null;
        var known = await dbContext.PlatformFeatures.Select(feature => feature.FeatureKey).ToListAsync(cancellationToken);
        var unknown = features.Except(known, StringComparer.Ordinal).ToList();
        return unknown.Count == 0 ? null : $"Unknown features: {string.Join(", ", unknown)}.";
    }

    /// <summary>Набор функций тарифа целиком: каждая функция платформы — включена или нет.</summary>
    private async Task SetFeaturesAsync(string planCode, IReadOnlyList<string>? included, CancellationToken cancellationToken)
    {
        if (included is null) return;
        var chosen = included.ToHashSet(StringComparer.Ordinal);
        var rows = await dbContext.PlanFeatures.Where(row => row.PlanCode == planCode).ToDictionaryAsync(row => row.FeatureKey, cancellationToken);
        foreach (var featureKey in await dbContext.PlatformFeatures.Select(feature => feature.FeatureKey).ToListAsync(cancellationToken))
        {
            if (!rows.TryGetValue(featureKey, out var row))
            {
                row = new PlanFeatureEntity { PlanFeatureId = Guid.NewGuid(), PlanCode = planCode, FeatureKey = featureKey };
                dbContext.PlanFeatures.Add(row);
            }

            row.IsIncluded = chosen.Contains(featureKey);
        }
    }

    private async Task<IReadOnlyList<SubscriptionPlanDto>> DescribeAsync(
        IReadOnlyList<SubscriptionPlanEntity> plans, CancellationToken cancellationToken)
    {
        var codes = plans.Select(plan => plan.PlanCode).ToList();
        var features = await dbContext.PlatformFeatures.AsNoTracking().OrderBy(feature => feature.FeatureKey).ToListAsync(cancellationToken);
        var rows = await dbContext.PlanFeatures.AsNoTracking().Where(row => codes.Contains(row.PlanCode)).ToListAsync(cancellationToken);
        var clubs = await dbContext.Organizations.AsNoTracking()
            .Where(organization => codes.Contains(organization.PlanCode))
            .GroupBy(organization => organization.PlanCode)
            .Select(group => new { PlanCode = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.PlanCode, group => group.Count, cancellationToken);

        return plans.Select(plan => ToDto(plan) with
        {
            // Строки тарифа нет — решает значение функции по умолчанию, как и у клубов.
            Features = features.Select(feature => new PlanFeatureDto(
                feature.FeatureKey,
                feature.Name,
                rows.FirstOrDefault(row => row.PlanCode == plan.PlanCode && row.FeatureKey == feature.FeatureKey)?.IsIncluded
                    ?? feature.EnabledByDefault)).ToList(),
            Clubs = clubs.GetValueOrDefault(plan.PlanCode)
        }).ToList();
    }

    private static SubscriptionPlanDto ToDto(SubscriptionPlanEntity entity) =>
        new(
            PlanCode: entity.PlanCode,
            Name: entity.Name,
            PriceMinorUnits: entity.PriceMinorUnits,
            CurrencyCode: entity.CurrencyCode,
            BillingInterval: entity.BillingInterval,
            MaxBranches: entity.MaxBranches,
            MaxDevicesPerBranch: entity.MaxDevicesPerBranch,
            MaxConcurrentSessions: entity.MaxConcurrentSessions,
            MaxStaffUsersPerBranch: entity.MaxStaffUsersPerBranch,
            IsActive: entity.IsActive,
            SortOrder: entity.SortOrder,
            PricePerDeviceMinorUnits: entity.PricePerDeviceMinorUnits,
            IncludedDevices: entity.IncludedDevices,
            MaxDevices: entity.MaxDevices);
}
