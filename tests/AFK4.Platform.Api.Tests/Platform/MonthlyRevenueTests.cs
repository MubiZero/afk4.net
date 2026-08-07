using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class MonthlyRevenueTests
{
    private static readonly DateOnly First = new(2026, 1, 1);
    private static readonly DateOnly Last = new(2026, 12, 1);

    private static InvoiceRevenueRow Subscription(int year, int month, long amount, int months = 1) =>
        new(InvoiceKindNames.Subscription, InvoiceStatusNames.Issued,
            new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(months),
            amount);

    [Fact]
    public void MonthlyInvoice_LandsInItsOwnMonth()
    {
        var points = MonthlyRevenue.Spread([Subscription(2026, 3, 290000)], First, Last);

        var march = points.Single(point => point.Month == 3);
        Assert.Equal(290000, march.RecurringMinorUnits);
        Assert.All(points.Where(point => point.Month != 3), point => Assert.Equal(0, point.RecurringMinorUnits));
    }

    [Fact]
    public void YearlyInvoice_SpreadsAcrossTwelveMonths()
    {
        var points = MonthlyRevenue.Spread([Subscription(2026, 1, 3480000, months: 12)], First, Last);

        Assert.All(points, point => Assert.Equal(290000, point.RecurringMinorUnits));
        Assert.Equal(3480000, points.Sum(point => point.RecurringMinorUnits));
    }

    [Fact]
    public void SpreadRemainder_GoesToTheFirstMonths_SoTheTotalIsExact()
    {
        // 100 сомони на 3 месяца не делится нацело: сумма частей обязана сойтись с суммой счёта,
        // иначе годовая выручка в отчёте не сойдётся с выставленным.
        var invoice = new InvoiceRevenueRow(
            InvoiceKindNames.Subscription, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            10000);

        var points = MonthlyRevenue.Spread([invoice], First, Last);

        Assert.Equal(10000, points.Sum(point => point.RecurringMinorUnits));
        Assert.Equal(3334, points.Single(point => point.Month == 1).RecurringMinorUnits);
        Assert.Equal(3333, points.Single(point => point.Month == 3).RecurringMinorUnits);
    }

    [Fact]
    public void VoidedInvoice_IsIgnored()
    {
        var voided = Subscription(2026, 3, 290000) with { Status = InvoiceStatusNames.Void };

        var points = MonthlyRevenue.Spread([voided], First, Last);

        Assert.All(points, point => Assert.Equal(0, point.RecurringMinorUnits));
    }

    [Fact]
    public void OneOff_CountsSeparatelyFromRecurring()
    {
        var oneOff = new InvoiceRevenueRow(
            InvoiceKindNames.OneOff, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            50000);

        var points = MonthlyRevenue.Spread([oneOff], First, Last);

        var may = points.Single(point => point.Month == 5);
        Assert.Equal(0, may.RecurringMinorUnits);
        Assert.Equal(50000, may.OneOffMinorUnits);
    }

    [Fact]
    public void Proration_CountsAsOneOff_NotSpreadAcrossItsPeriod()
    {
        // Proration тоже несёт Period-поля (как subscription), но это разовая доплата за уже
        // прошедший кусок месяца — она должна лечь ЦЕЛИКОМ в месяц выставления, а не размазаться.
        var proration = new InvoiceRevenueRow(
            InvoiceKindNames.Proration, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(3),
            15000);

        var points = MonthlyRevenue.Spread([proration], First, Last);

        var july = points.Single(point => point.Month == 7);
        Assert.Equal(0, july.RecurringMinorUnits);
        Assert.Equal(15000, july.OneOffMinorUnits);
        Assert.All(points.Where(point => point.Month != 7), point => Assert.Equal(0, point.OneOffMinorUnits));
    }

    [Fact]
    public void CreditNote_ReducesTheMonthItBelongsTo()
    {
        // Кредит-нота — отрицательная сумма, а не отдельный флаг: выручка месяца должна уменьшиться.
        var credit = new InvoiceRevenueRow(
            InvoiceKindNames.OneOff, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            -20000);

        var points = MonthlyRevenue.Spread([credit], First, Last);

        Assert.Equal(-20000, points.Single(point => point.Month == 6).OneOffMinorUnits);
    }

    [Fact]
    public void CreditKind_ReducesTheMonthItBelongsTo()
    {
        // Реальные кредит-ноты в базе идут с Kind = Credit (см. InvoiceKindNames.Credit), а не OneOff.
        // Сегодня это работает через ту же нерекуррентную ветку, что proration/one_off — этот тест
        // ловит регрессию, если Credit когда-нибудь получит собственную обработку.
        var credit = new InvoiceRevenueRow(
            InvoiceKindNames.Credit, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            -20000);

        var points = MonthlyRevenue.Spread([credit], First, Last);

        var august = points.Single(point => point.Month == 8);
        Assert.Equal(0, august.RecurringMinorUnits);
        Assert.Equal(-20000, august.OneOffMinorUnits);
    }

    [Fact]
    public void MonthsOutsideTheWindow_AreClipped_NotDropped()
    {
        // Годовой счёт, начавшийся до окна: в окно попадают только его месяцы, лежащие внутри.
        var points = MonthlyRevenue.Spread([Subscription(2025, 7, 1200000, months: 12)], First, Last);

        Assert.Equal(600000, points.Sum(point => point.RecurringMinorUnits));
        Assert.Equal(100000, points.Single(point => point.Month == 1).RecurringMinorUnits);
        Assert.Equal(0, points.Single(point => point.Month == 7).RecurringMinorUnits);
    }

    [Fact]
    public void EveryMonthOfTheWindow_IsPresent_EvenWithoutInvoices()
    {
        var points = MonthlyRevenue.Spread([], First, Last);

        Assert.Equal(12, points.Count);
        Assert.All(points, point => Assert.Equal(2026, point.Year));
    }
}
