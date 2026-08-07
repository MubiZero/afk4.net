using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed record InvoiceRevenueRow(
    string Kind,
    string Status,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    long AmountMinorUnits);

public sealed record MonthlyRevenuePoint(int Year, int Month, long RecurringMinorUnits, long OneOffMinorUnits);

/// <summary>
/// Раскладывает выставленные счета по календарным месяцам. Считаем ИЗ СЧЕТОВ, а не накапливаем
/// отдельную метрику: у счёта есть период и сумма, поэтому цифра всегда сходится с тем, что реально
/// выставлено, и не может разойтись с биллингом.
/// </summary>
public static class MonthlyRevenue
{
    public static IReadOnlyList<MonthlyRevenuePoint> Spread(
        IReadOnlyCollection<InvoiceRevenueRow> invoices, DateOnly firstMonth, DateOnly lastMonth)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var recurring = new Dictionary<(int Year, int Month), long>();
        var oneOff = new Dictionary<(int Year, int Month), long>();

        foreach (var invoice in invoices)
        {
            // Аннулированный счёт не выручка: деньги по нему не ждут и не придут.
            if (invoice.Status == InvoiceStatusNames.Void) continue;

            var isRecurring = invoice.Kind == InvoiceKindNames.Subscription;
            var target = isRecurring ? recurring : oneOff;

            if (!isRecurring)
            {
                // Разовые счета, доплаты и кредит-ноты не растягиваются: они относятся к моменту,
                // а не к периоду.
                Add(target, invoice.PeriodStartUtc.Year, invoice.PeriodStartUtc.Month, invoice.AmountMinorUnits);
                continue;
            }

            var months = MonthsOf(invoice).ToList();
            if (months.Count == 0)
            {
                Add(target, invoice.PeriodStartUtc.Year, invoice.PeriodStartUtc.Month, invoice.AmountMinorUnits);
                continue;
            }

            // Остаток от деления кладём в первые месяцы, чтобы сумма частей ТОЧНО равнялась сумме
            // счёта: отчёт, где годовая выручка не сходится с выставленным, бесполезен.
            var share = invoice.AmountMinorUnits / months.Count;
            var remainder = invoice.AmountMinorUnits - share * months.Count;
            for (var index = 0; index < months.Count; index++)
            {
                var extra = index < Math.Abs(remainder) ? Math.Sign(remainder) : 0;
                Add(target, months[index].Year, months[index].Month, share + extra);
            }
        }

        var points = new List<MonthlyRevenuePoint>();
        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            var key = (month.Year, month.Month);
            points.Add(new MonthlyRevenuePoint(
                month.Year,
                month.Month,
                recurring.GetValueOrDefault(key),
                oneOff.GetValueOrDefault(key)));
        }

        return points;
    }

    private static IEnumerable<(int Year, int Month)> MonthsOf(InvoiceRevenueRow invoice)
    {
        var start = new DateOnly(invoice.PeriodStartUtc.Year, invoice.PeriodStartUtc.Month, 1);
        var endExclusive = new DateOnly(invoice.PeriodEndUtc.Year, invoice.PeriodEndUtc.Month, 1);
        if (endExclusive <= start) yield break;

        for (var month = start; month < endExclusive; month = month.AddMonths(1))
            yield return (month.Year, month.Month);
    }

    private static void Add(Dictionary<(int Year, int Month), long> target, int year, int month, long amount) =>
        target[(year, month)] = target.GetValueOrDefault((year, month)) + amount;
}
