using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfTenantSubscriptionService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : ITenantSubscriptionService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        SubscriptionStatusNames.Trial,
        SubscriptionStatusNames.Active,
        SubscriptionStatusNames.PastDue,
        SubscriptionStatusNames.Cancelled
    };

    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.Ordinal)
    {
        BillingIntervalNames.Monthly,
        BillingIntervalNames.Yearly
    };

    public async Task<BillingOperationResult<TenantSubscriptionDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.OrganizationId == organizationId, cancellationToken);
        if (org is null)
        {
            return BillingOperationResult<TenantSubscriptionDto>.NotFound("Tenant was not found.");
        }

        var subscription = await EnsureSubscriptionAsync(org, cancellationToken);
        return BillingOperationResult<TenantSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<BillingOperationResult<TenantSubscriptionDto>> UpdateAsync(
        Guid organizationId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.OrganizationId == organizationId, cancellationToken);
        if (org is null)
        {
            return BillingOperationResult<TenantSubscriptionDto>.NotFound("Tenant was not found.");
        }

        if (request.Status is not null && !AllowedStatuses.Contains(request.Status.Trim()))
        {
            return BillingOperationResult<TenantSubscriptionDto>.BadRequest(
                $"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        if (request.BillingInterval is not null && !AllowedIntervals.Contains(request.BillingInterval.Trim()))
        {
            return BillingOperationResult<TenantSubscriptionDto>.BadRequest(
                $"BillingInterval must be one of: {string.Join(", ", AllowedIntervals)}.");
        }

        var subscription = await EnsureSubscriptionAsync(org, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (request.PlanCode is not null && request.PlanCode.Trim() != subscription.PlanCode)
        {
            var newPlan = await dbContext.SubscriptionPlans
                .SingleOrDefaultAsync(plan => plan.PlanCode == request.PlanCode.Trim(), cancellationToken);
            if (newPlan is null)
            {
                return BillingOperationResult<TenantSubscriptionDto>.BadRequest(
                    $"Plan '{request.PlanCode.Trim()}' was not found.");
            }

            var newInterval = request.BillingInterval?.Trim() ?? newPlan.BillingInterval;
            var proration = ComputeProration(
                subscription.AmountMinorUnits,
                newPlan.PriceMinorUnits,
                subscription.CurrentPeriodStartUtc,
                subscription.CurrentPeriodEndUtc,
                now);
            if (proration > 0)
            {
                dbContext.Invoices.Add(new InvoiceEntity
                {
                    InvoiceId = Guid.NewGuid(),
                    OrganizationId = org.OrganizationId,
                    Number = await NextInvoiceNumberAsync(cancellationToken),
                    Kind = InvoiceKindNames.Proration,
                    PeriodStartUtc = now,
                    PeriodEndUtc = subscription.CurrentPeriodEndUtc,
                    IssuedAtUtc = now,
                    DueAtUtc = now.AddDays(7),
                    AmountMinorUnits = proration,
                    CurrencyCode = newPlan.CurrencyCode,
                    Status = InvoiceStatusNames.Issued,
                    Description = $"Proration: {subscription.PlanCode} → {newPlan.PlanCode}",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            subscription.PlanCode = newPlan.PlanCode;
            subscription.AmountMinorUnits = newPlan.PriceMinorUnits;
            subscription.CurrencyCode = newPlan.CurrencyCode;
            subscription.BillingInterval = newInterval;

            org.LimitsJson = JsonSerializer.Serialize(new TenantLimitsDto(
                newPlan.MaxBranches,
                newPlan.MaxDevicesPerBranch,
                newPlan.MaxConcurrentSessions,
                newPlan.MaxStaffUsersPerBranch));
            org.PlanCode = newPlan.PlanCode;
        }
        else if (request.BillingInterval is not null)
        {
            subscription.BillingInterval = request.BillingInterval.Trim();
        }

        if (request.Status is not null)
        {
            subscription.Status = request.Status.Trim();
            org.SubscriptionStatus = subscription.Status;
        }

        if (request.CancelAtPeriodEnd is not null)
        {
            subscription.CancelAtPeriodEnd = request.CancelAtPeriodEnd.Value;
        }

        subscription.UpdatedAtUtc = now;
        org.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<TenantSubscriptionDto>.Success(ToDto(subscription));
    }

    private async Task<TenantSubscriptionEntity> EnsureSubscriptionAsync(
        OrganizationEntity org,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(subscription => subscription.OrganizationId == org.OrganizationId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var plan = await dbContext.SubscriptionPlans
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == org.PlanCode, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var interval = plan?.BillingInterval ?? BillingIntervalNames.Monthly;
        var subscription = new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = org.OrganizationId,
            PlanCode = org.PlanCode,
            Status = org.SubscriptionStatus,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = BillingPeriod.Advance(now, interval),
            NextInvoiceUtc = BillingPeriod.Advance(now, interval),
            AmountMinorUnits = plan?.PriceMinorUnits ?? 0,
            CurrencyCode = plan?.CurrencyCode ?? "RUB",
            BillingInterval = interval,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.TenantSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private async Task<int> NextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var max = await dbContext.Invoices
            .Select(invoice => (int?)invoice.Number)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    internal static long ComputeProration(
        long oldAmount,
        long newAmount,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset now)
    {
        var totalDays = (periodEnd - periodStart).TotalDays;
        if (totalDays <= 0)
        {
            return 0;
        }

        var remainingDays = Math.Max(0, (periodEnd - now).TotalDays);
        var oldDaily = oldAmount / totalDays;
        var newDaily = newAmount / totalDays;
        return (long)Math.Round((newDaily - oldDaily) * remainingDays, MidpointRounding.AwayFromZero);
    }

    private static TenantSubscriptionDto ToDto(TenantSubscriptionEntity entity) =>
        new(
            TenantSubscriptionId: entity.TenantSubscriptionId,
            OrganizationId: entity.OrganizationId,
            PlanCode: entity.PlanCode,
            Status: entity.Status,
            CurrentPeriodStartUtc: entity.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc: entity.CurrentPeriodEndUtc,
            NextInvoiceUtc: entity.NextInvoiceUtc,
            AmountMinorUnits: entity.AmountMinorUnits,
            CurrencyCode: entity.CurrencyCode,
            BillingInterval: entity.BillingInterval,
            CancelAtPeriodEnd: entity.CancelAtPeriodEnd,
            CreatedAtUtc: entity.CreatedAtUtc,
            UpdatedAtUtc: entity.UpdatedAtUtc);
}
