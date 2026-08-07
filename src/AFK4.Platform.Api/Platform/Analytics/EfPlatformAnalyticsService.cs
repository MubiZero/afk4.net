using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class EfPlatformAnalyticsService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlatformAnalyticsService
{
    public const int MinMonths = 3;
    public const int MaxMonths = 36;
    private const string DefaultCurrency = "TJS";

    public async Task<PlatformAnalyticsOverviewDto> GetOverviewAsync(int months, CancellationToken cancellationToken)
    {
        var window = Math.Clamp(months, MinMonths, MaxMonths);
        var now = timeProvider.GetUtcNow();
        var lastMonth = new DateOnly(now.Year, now.Month, 1);
        var firstMonth = lastMonth.AddMonths(-(window - 1));
        var windowStart = new DateTimeOffset(firstMonth.Year, firstMonth.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // Счета берём с запасом назад: годовой счёт, выставленный до окна, всё ещё отдаёт
        // в окно свои месяцы — без запаса выручка начала окна была бы занижена.
        var invoiceCutoff = windowStart.AddMonths(-12);
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.PeriodEndUtc >= invoiceCutoff)
            .Select(invoice => new InvoiceRevenueRow(
                invoice.Kind, invoice.Status, invoice.PeriodStartUtc, invoice.PeriodEndUtc, invoice.AmountMinorUnits))
            .ToListAsync(cancellationToken);

        var snapshots = await dbContext.SubscriptionDailySnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.SnapshotDate >= firstMonth.AddMonths(-1))
            .Select(snapshot => new SnapshotRow(snapshot.OrganizationId, snapshot.SnapshotDate, snapshot.Status))
            .ToListAsync(cancellationToken);

        var revenue = MonthlyRevenue.Spread(invoices, firstMonth, lastMonth);
        var movement = SubscriptionMovement.Compute(snapshots, firstMonth, lastMonth);
        var movementByMonth = movement.ToDictionary(point => (point.Year, point.Month));

        var monthDtos = revenue
            .Select(point =>
            {
                var moves = movementByMonth.GetValueOrDefault((point.Year, point.Month))
                    ?? new MovementPoint(point.Year, point.Month, 0, 0, 0);
                return new AnalyticsMonthDto(
                    point.Year, point.Month,
                    point.RecurringMinorUnits, point.OneOffMinorUnits,
                    moves.Joined, moves.Left, moves.PayingAtMonthEnd);
            })
            .ToList();

        var payingSubscriptions = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Status == SubscriptionStatusNames.Active
                || subscription.Status == SubscriptionStatusNames.PastDue)
            .Select(subscription => new { subscription.AmountMinorUnits, subscription.BillingInterval, subscription.CurrencyCode })
            .ToListAsync(cancellationToken);

        var currentMrr = payingSubscriptions.Sum(subscription => subscription.BillingInterval == BillingIntervalNames.Yearly
            ? subscription.AmountMinorUnits / 12
            : subscription.AmountMinorUnits);

        var outstanding = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
            .SumAsync(invoice => invoice.AmountMinorUnits, cancellationToken);

        return new PlatformAnalyticsOverviewDto(
            GeneratedAtUtc: now,
            CurrencyCode: payingSubscriptions.Count > 0 ? payingSubscriptions[0].CurrencyCode : DefaultCurrency,
            Months: monthDtos,
            CurrentMrrMinorUnits: currentMrr,
            CurrentPayingClubs: payingSubscriptions.Count,
            // Средний чек на платящий клуб; без платящих клубов это ноль, а не деление на ноль.
            AverageRevenuePerClubMinorUnits: payingSubscriptions.Count > 0 ? currentMrr / payingSubscriptions.Count : 0,
            OutstandingMinorUnits: outstanding);
    }
}
