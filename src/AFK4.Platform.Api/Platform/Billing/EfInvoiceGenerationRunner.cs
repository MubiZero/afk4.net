using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceGenerationRunner(
    PlatformDbContext dbContext,
    IOptions<BillingOptions> options,
    IInvoiceNotifier invoiceNotifier) : IInvoiceGenerationRunner
{
    private readonly BillingOptions options = options.Value;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var dueSubscriptions = await dbContext.OrganizationSubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatusNames.Active &&
                subscription.NextInvoiceUtc != null &&
                subscription.NextInvoiceUtc <= now)
            .ToListAsync(cancellationToken);

        var issued = 0;
        foreach (var subscription in dueSubscriptions)
        {
            var invoice = await GenerateForSubscriptionAsync(subscription, now, cancellationToken);
            if (invoice is not null)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await invoiceNotifier.NotifyIssuedAsync(invoice, cancellationToken);
                issued++;
            }
        }

        return issued;
    }

    public async Task<InvoiceEntity?> GenerateForSubscriptionAsync(
        OrganizationSubscriptionEntity subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var alreadyIssued = await dbContext.Invoices.AnyAsync(invoice =>
            invoice.OrganizationId == subscription.OrganizationId &&
            invoice.Kind == InvoiceKindNames.Subscription &&
            invoice.PeriodStartUtc == subscription.CurrentPeriodStartUtc &&
            invoice.Status != InvoiceStatusNames.Void,
            cancellationToken);
        if (alreadyIssued)
        {
            return null;
        }

        var number = ((await dbContext.Invoices
            .Select(invoice => (int?)invoice.Number)
            .MaxAsync(cancellationToken)) ?? 0) + 1;

        var gross = subscription.AmountMinorUnits;
        var discountApplies = subscription.DiscountUntilUtc is null || subscription.DiscountUntilUtc > now;
        var discount = discountApplies
            ? SubscriptionDiscount.Apply(gross, subscription.DiscountPercent, subscription.DiscountAmountMinorUnits)
            : 0;

        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = subscription.OrganizationId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = subscription.CurrentPeriodStartUtc,
            PeriodEndUtc = subscription.CurrentPeriodEndUtc,
            IssuedAtUtc = now,
            DueAtUtc = now.Add(options.InvoiceDueAfter),
            AmountMinorUnits = gross - discount,
            GrossAmountMinorUnits = gross,
            DiscountMinorUnits = discount,
            CurrencyCode = subscription.CurrencyCode,
            Status = InvoiceStatusNames.Issued,
            Description = $"Subscription {subscription.PlanCode} " +
                $"({subscription.CurrentPeriodStartUtc:yyyy-MM-dd} – {subscription.CurrentPeriodEndUtc:yyyy-MM-dd})",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Invoices.Add(invoice);

        subscription.CurrentPeriodStartUtc = subscription.CurrentPeriodEndUtc;
        subscription.CurrentPeriodEndUtc = BillingPeriod.Advance(subscription.CurrentPeriodEndUtc, subscription.BillingInterval);
        subscription.NextInvoiceUtc = subscription.CurrentPeriodEndUtc;
        subscription.UpdatedAtUtc = now;
        return invoice;
    }
}
