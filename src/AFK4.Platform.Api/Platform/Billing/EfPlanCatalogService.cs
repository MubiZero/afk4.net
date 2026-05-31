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
        return plans.Select(ToDto).ToList();
    }

    public async Task<SubscriptionPlanDto?> GetAsync(string planCode, CancellationToken cancellationToken)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == normalized, cancellationToken);
        return plan is null ? null : ToDto(plan);
    }

    public async Task<BillingOperationResult<SubscriptionPlanDto>> CreateAsync(
        CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var planCode = (request.PlanCode ?? string.Empty).Trim();
        var validationError = ValidateCommon(planCode, request.Name, request.CurrencyCode, request.BillingInterval, request.PriceMinorUnits);
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
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.SubscriptionPlans.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<SubscriptionPlanDto>.Success(ToDto(entity));
    }

    public async Task<BillingOperationResult<SubscriptionPlanDto>> UpdateAsync(
        string planCode,
        UpdatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var validationError = ValidateCommon(normalized, request.Name, request.CurrencyCode, request.BillingInterval, request.PriceMinorUnits);
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
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<SubscriptionPlanDto>.Success(ToDto(entity));
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
            SortOrder: entity.SortOrder);
}
