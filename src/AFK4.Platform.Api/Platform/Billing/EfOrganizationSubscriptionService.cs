using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfOrganizationSubscriptionService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IOrganizationSubscriptionService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        SubscriptionStatusNames.Trial,
        SubscriptionStatusNames.Active,
        SubscriptionStatusNames.PastDue,
        SubscriptionStatusNames.Cancelled
    };

    // Matches OrganizationSubscriptionEntity.VoidReason-style siblings (InvoiceEntity.VoidReason is
    // 512); DiscountReason had no cap at all before this fix.
    private const int MaxDiscountReasonLength = 512;

    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.Ordinal)
    {
        BillingIntervalNames.Monthly,
        BillingIntervalNames.Yearly
    };

    public async Task<BillingOperationResult<OrganizationSubscriptionDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.OrganizationId == organizationId, cancellationToken);
        if (org is null)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.NotFound("Organization was not found.");
        }

        var subscription = await EnsureSubscriptionAsync(org, cancellationToken);
        return BillingOperationResult<OrganizationSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<BillingOperationResult<OrganizationSubscriptionDto>> UpdateAsync(
        Guid organizationId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.OrganizationId == organizationId, cancellationToken);
        if (org is null)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.NotFound("Organization was not found.");
        }

        if (request.Status is not null && !AllowedStatuses.Contains(request.Status.Trim()))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                $"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        if (request.BillingInterval is not null && !AllowedIntervals.Contains(request.BillingInterval.Trim()))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                $"BillingInterval must be one of: {string.Join(", ", AllowedIntervals)}.");
        }

        if (request.AmountMinorUnits is < 0)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "AmountMinorUnits must not be negative.");
        }

        if (request.Status is not null
            && request.Status.Trim() == SubscriptionStatusNames.Trial
            && request.CurrentPeriodEndUtc is null)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "CurrentPeriodEndUtc must be provided when moving a subscription to trial.");
        }

        var subscription = await EnsureSubscriptionAsync(org, cancellationToken);
        var now = timeProvider.GetUtcNow();
        InvoiceEntity? prorationInvoice = null;

        var newPeriodEnd = request.CurrentPeriodEndUtc ?? subscription.CurrentPeriodEndUtc;
        if (newPeriodEnd <= subscription.CurrentPeriodStartUtc)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "CurrentPeriodEndUtc must be later than CurrentPeriodStartUtc.");
        }

        if (request.PaymentGraceUntilUtc is not null && request.PaymentGraceUntilUtc <= now)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "PaymentGraceUntilUtc must be in the future.");
        }

        if (request.ClearPaymentGrace == true && request.PaymentGraceUntilUtc is not null)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "ClearPaymentGrace and PaymentGraceUntilUtc must not be set together.");
        }

        if (request.DiscountPercent is not null && request.DiscountAmountMinorUnits is not null)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "Set either DiscountPercent or DiscountAmountMinorUnits, not both.");
        }

        if (request.DiscountPercent is not null and (< 1 or > 100))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "DiscountPercent must be between 1 and 100.");
        }

        if (request.DiscountAmountMinorUnits is not null and < 1)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "DiscountAmountMinorUnits must be positive.");
        }

        if (request.ClearDiscount == true
            && (request.DiscountPercent is not null || request.DiscountAmountMinorUnits is not null))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "ClearDiscount and discount values must not be set together.");
        }

        if (request.ClearDiscount == true && !string.IsNullOrWhiteSpace(request.DiscountReason))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "ClearDiscount and DiscountReason must not be set together.");
        }

        if (request.DiscountReason is not null && request.DiscountReason.Trim().Length > MaxDiscountReasonLength)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                $"DiscountReason must be at most {MaxDiscountReasonLength} characters.");
        }

        if (request.PlanCode is not null && request.PlanCode.Trim() != subscription.PlanCode)
        {
            var newPlan = await dbContext.SubscriptionPlans
                .SingleOrDefaultAsync(plan => plan.PlanCode == request.PlanCode.Trim(), cancellationToken);
            if (newPlan is null)
            {
                return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
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
                prorationInvoice = new InvoiceEntity
                {
                    InvoiceId = Guid.NewGuid(),
                    OrganizationId = org.OrganizationId,
                    Number = await InvoiceNumbering.NextNumberAsync(dbContext, cancellationToken),
                    Kind = InvoiceKindNames.Proration,
                    PeriodStartUtc = now,
                    PeriodEndUtc = subscription.CurrentPeriodEndUtc,
                    IssuedAtUtc = now,
                    DueAtUtc = now.AddDays(7),
                    AmountMinorUnits = proration,
                    GrossAmountMinorUnits = proration,
                    CurrencyCode = newPlan.CurrencyCode,
                    Status = InvoiceStatusNames.Issued,
                    Description = $"Proration: {subscription.PlanCode} → {newPlan.PlanCode}",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                dbContext.Invoices.Add(prorationInvoice);
            }

            subscription.PlanCode = newPlan.PlanCode;
            subscription.AmountMinorUnits = newPlan.PriceMinorUnits;
            subscription.CurrencyCode = newPlan.CurrencyCode;
            subscription.BillingInterval = newInterval;

            org.LimitsJson = JsonSerializer.Serialize(new OrganizationLimitsDto(
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

        if (request.AmountMinorUnits is not null)
        {
            subscription.AmountMinorUnits = request.AmountMinorUnits.Value;
        }

        if (request.CurrentPeriodEndUtc is not null)
        {
            subscription.CurrentPeriodEndUtc = request.CurrentPeriodEndUtc.Value;
        }

        if (request.PaymentGraceUntilUtc is not null)
        {
            subscription.PaymentGraceUntilUtc = request.PaymentGraceUntilUtc.Value;
        }
        else if (request.ClearPaymentGrace == true)
        {
            subscription.PaymentGraceUntilUtc = null;
        }

        if (request.ClearDiscount == true)
        {
            subscription.DiscountPercent = null;
            subscription.DiscountAmountMinorUnits = null;
            subscription.DiscountUntilUtc = null;
            subscription.DiscountReason = null;
        }
        else
        {
            if (request.DiscountPercent is not null)
            {
                subscription.DiscountPercent = request.DiscountPercent;
                subscription.DiscountAmountMinorUnits = null;
            }

            if (request.DiscountAmountMinorUnits is not null)
            {
                subscription.DiscountAmountMinorUnits = request.DiscountAmountMinorUnits;
                subscription.DiscountPercent = null;
            }

            if (request.DiscountUntilUtc is not null)
            {
                subscription.DiscountUntilUtc = request.DiscountUntilUtc;
            }

            if (request.DiscountReason is not null)
            {
                subscription.DiscountReason = request.DiscountReason.Trim();
            }
        }

        subscription.UpdatedAtUtc = now;
        org.UpdatedAtUtc = now;

        if (prorationInvoice is not null)
        {
            try
            {
                await InvoiceNumbering.SaveAsync(dbContext, prorationInvoice, cancellationToken);
            }
            catch (InvoiceNumberAllocationException)
            {
                return BillingOperationResult<OrganizationSubscriptionDto>.Conflict(
                    "Could not issue the proration invoice because of a numbering conflict with another concurrent request. Please retry.");
            }
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return BillingOperationResult<OrganizationSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>> ListAsync(
        string? status,
        string? planCode,
        CancellationToken cancellationToken)
    {
        if (status is not null && !AllowedStatuses.Contains(status.Trim()))
        {
            return BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>.BadRequest(
                $"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        var query =
            from subscription in dbContext.OrganizationSubscriptions.AsNoTracking()
            join org in dbContext.Organizations.AsNoTracking()
                on subscription.OrganizationId equals org.OrganizationId
            select new { subscription, org.Name, org.Slug };

        if (status is not null)
        {
            var s = status.Trim();
            query = query.Where(x => x.subscription.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            var p = planCode.Trim();
            query = query.Where(x => x.subscription.PlanCode == p);
        }

        var rows = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<SubscriptionListItemDto> dtos = rows.Select(x => new SubscriptionListItemDto(
            OrganizationSubscriptionId: x.subscription.OrganizationSubscriptionId,
            OrganizationId: x.subscription.OrganizationId,
            OrganizationName: x.Name,
            OrganizationSlug: x.Slug,
            PlanCode: x.subscription.PlanCode,
            Status: x.subscription.Status,
            BillingInterval: x.subscription.BillingInterval,
            AmountMinorUnits: x.subscription.AmountMinorUnits,
            CurrencyCode: x.subscription.CurrencyCode,
            CurrentPeriodEndUtc: x.subscription.CurrentPeriodEndUtc,
            NextInvoiceUtc: x.subscription.NextInvoiceUtc,
            CancelAtPeriodEnd: x.subscription.CancelAtPeriodEnd)).ToList();

        return BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>.Success(dtos);
    }

    private async Task<OrganizationSubscriptionEntity> EnsureSubscriptionAsync(
        OrganizationEntity org,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.OrganizationSubscriptions
            .SingleOrDefaultAsync(subscription => subscription.OrganizationId == org.OrganizationId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var plan = await dbContext.SubscriptionPlans
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == org.PlanCode, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var interval = plan?.BillingInterval ?? BillingIntervalNames.Monthly;
        var subscription = new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = org.OrganizationId,
            PlanCode = org.PlanCode,
            Status = org.SubscriptionStatus,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = BillingPeriod.Advance(now, interval),
            NextInvoiceUtc = BillingPeriod.Advance(now, interval),
            AmountMinorUnits = plan?.PriceMinorUnits ?? 0,
            CurrencyCode = plan?.CurrencyCode ?? "TJS",
            BillingInterval = interval,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.OrganizationSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
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

    private static OrganizationSubscriptionDto ToDto(OrganizationSubscriptionEntity entity) =>
        new(
            OrganizationSubscriptionId: entity.OrganizationSubscriptionId,
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
            UpdatedAtUtc: entity.UpdatedAtUtc,
            PaymentGraceUntilUtc: entity.PaymentGraceUntilUtc,
            DiscountPercent: entity.DiscountPercent,
            DiscountAmountMinorUnits: entity.DiscountAmountMinorUnits,
            DiscountUntilUtc: entity.DiscountUntilUtc,
            DiscountReason: entity.DiscountReason);
}
