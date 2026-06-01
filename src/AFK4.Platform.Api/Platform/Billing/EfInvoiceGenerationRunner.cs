using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
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
        var dueSubscriptions = await dbContext.TenantSubscriptions
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

        var overdue = await dbContext.Invoices
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued && invoice.DueAtUtc < now)
            .ToListAsync(cancellationToken);
        foreach (var invoice in overdue)
        {
            invoice.Status = InvoiceStatusNames.Overdue;
            invoice.UpdatedAtUtc = now;
        }

        if (overdue.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var invoice in overdue)
            {
                await invoiceNotifier.NotifyOverdueAsync(invoice, cancellationToken);
            }
        }

        return issued;
    }

    public async Task<InvoiceEntity?> GenerateForSubscriptionAsync(
        TenantSubscriptionEntity subscription,
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
            AmountMinorUnits = subscription.AmountMinorUnits,
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
