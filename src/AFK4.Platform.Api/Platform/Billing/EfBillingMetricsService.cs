using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfBillingMetricsService(PlatformDbContext dbContext) : IBillingMetricsService
{
    private const string DefaultCurrency = "TJS";

    public async Task<PlatformBillingMetricsDto> GetAsync(CancellationToken cancellationToken)
    {
        // MRR counts active subscriptions only (locked plan definition); trial/past_due/cancelled are excluded.
        var activeSubscriptions = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatusNames.Active)
            .Select(s => new { s.AmountMinorUnits, s.BillingInterval, s.CurrencyCode })
            .ToListAsync(cancellationToken);

        long mrr = 0;
        foreach (var s in activeSubscriptions)
        {
            mrr += s.BillingInterval == BillingIntervalNames.Yearly
                ? s.AmountMinorUnits / 12
                : s.AmountMinorUnits;
        }

        var currency = activeSubscriptions.Count > 0 ? activeSubscriptions[0].CurrencyCode : DefaultCurrency;

        var outstanding = await dbContext.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatusNames.Issued || i.Status == InvoiceStatusNames.Overdue)
            .Select(i => new { i.AmountMinorUnits, i.Status })
            .ToListAsync(cancellationToken);

        var outstandingTotal = outstanding.Sum(i => i.AmountMinorUnits);
        var overdue = outstanding.Where(i => i.Status == InvoiceStatusNames.Overdue).ToList();

        return new PlatformBillingMetricsDto(
            MrrMinorUnits: mrr,
            CurrencyCode: currency,
            ActiveSubscriptions: activeSubscriptions.Count,
            OutstandingMinorUnits: outstandingTotal,
            OutstandingCount: outstanding.Count,
            OverdueMinorUnits: overdue.Sum(i => i.AmountMinorUnits),
            OverdueCount: overdue.Count);
    }
}
