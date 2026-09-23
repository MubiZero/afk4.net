using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Audit;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reports;

public sealed class OrganizationAdminReportService(
    PlatformDbContext dbContext,
    IReportService reports) : IOrganizationAdminReportService
{
    private const int DetailLimit = 200;

    public async Task<OrganizationAdminSummaryReportDto> GetSummaryAsync(
        Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var period = await ResolvePeriodAsync(organizationId, branchId, fromDate, toDate, cancellationToken);
        var query = Query(period);
        var sales = await reports.GetSalesReportAsync(organizationId, branchId, query, cancellationToken);
        var gameplay = await reports.GetGameplayTimeReportAsync(organizationId, branchId, query, cancellationToken);
        var shifts = await reports.GetShiftReportAsync(organizationId, branchId, query, cancellationToken);

        var discrepancyAttention = shifts.Rows
            .Where(row => row.State == ShiftStateNames.Closed && row.Difference?.MinorUnits != 0)
            .Select(row => new OrganizationAdminReportAttentionDto(
                "shift_discrepancy", $"Shift {row.ShiftId:D}", "Closed shift cash discrepancy",
                row.ShiftId, row.Difference))
            .ToList();
        var criticalActions = new[] { AuditActionNames.RefundLedgerEntry, AuditActionNames.ManualLedgerCorrection, AuditActionNames.PayDebt, AuditActionNames.RefundPosSale, AuditActionNames.MoneyActionExecuted };
        var failedMoneyRecords = await dbContext.AuditRecords.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId &&
                row.CreatedAtUtc >= period.FromUtc && row.CreatedAtUtc < period.ToUtc &&
                row.Outcome == AuditOutcome.Denied && criticalActions.Contains(row.Action))
            .OrderByDescending(row => row.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var failedMoneyAttention = failedMoneyRecords.Select(row => new OrganizationAdminReportAttentionDto(
                "money_operation_failed", "Money operation needs review", row.Action,
                row.TargetId == null ? null : Guid.TryParse(row.TargetId, out var targetId) ? targetId : null,
                row.AmountMinorUnits == null ? null : new MoneyDto("TJS", row.AmountMinorUnits.Value)))
            .ToList();
        var attention = failedMoneyAttention.Concat(discrepancyAttention).ToList();
        var activeShift = await dbContext.Shifts.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId && row.State == ShiftStateNames.Open)
            .OrderByDescending(row => row.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var trend = new List<OrganizationAdminRevenueTrendPointDto>(7);
        var trendFromDate = toDate.AddDays(-6);
        var trendPeriod = await ResolvePeriodAsync(organizationId, branchId, trendFromDate, toDate, cancellationToken);
        var trendPayments = await PosMoneyAsync(organizationId, branchId, trendPeriod, cancellationToken);
        var trendGameplay = await GameplayMoneyAsync(organizationId, branchId, trendPeriod, cancellationToken);
        var periodGameplay = GameplayTotals.Of(await GameplayMoneyAsync(organizationId, branchId, period, cancellationToken));
        var periodPos = PosTotals.Of(await PosMoneyAsync(organizationId, branchId, period, cancellationToken));
        var trendTimeZone = TimeZoneInfo.FindSystemTimeZoneById(period.TimeZone);
        for (var offset = 6; offset >= 0; offset--)
        {
            var date = toDate.AddDays(-offset);
            var posRevenue = trendPayments.Where(row => LocalDate(row.CreatedAtUtc, trendTimeZone) == date).Sum(row => row.AmountMinorUnits);
            var gameplayRevenue = trendGameplay.Where(row => LocalDate(row.CreatedAtUtc, trendTimeZone) == date).Sum(row => GameplayImpact(row.EntryType, row.AmountMinorUnits));
            trend.Add(new(date, Money(sales.NetSalesTotal.CurrencyCode, posRevenue + gameplayRevenue)));
        }

        return new OrganizationAdminSummaryReportDto(
            Dto(period), attention.Count, attention.Take(3).ToList(),
            new OrganizationAdminReportFiguresDto(
                Money(sales.NetSalesTotal.CurrencyCode, periodPos.Net + periodGameplay.Net),
                Money(sales.NetSalesTotal.CurrencyCode, periodGameplay.Net),
                Money(sales.NetSalesTotal.CurrencyCode, periodPos.Net), gameplay.TotalDurationSeconds),
            trend,
            activeShift is null ? null : new OrganizationAdminActiveShiftDto(
                activeShift.ShiftId, activeShift.OpenedByStaffUserId, activeShift.OpenedAtUtc,
                Money(activeShift.CurrencyCode, activeShift.ExpectedCashMinorUnits), true));
    }

    public async Task<OrganizationAdminShiftCashReportDto> GetShiftCashAsync(
        Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var period = await ResolvePeriodAsync(organizationId, branchId, fromDate, toDate, cancellationToken);
        var query = Query(period);
        var shifts = await reports.GetShiftReportAsync(organizationId, branchId, query, cancellationToken);
        var cash = await reports.GetCashOperationReportAsync(organizationId, branchId, query, cancellationToken);
        return new OrganizationAdminShiftCashReportDto(Dto(period), shifts.Rows, cash.Rows, cash.CashInTotal, cash.CashOutTotal, cash.NetCashTotal);
    }

    public async Task<OrganizationAdminRevenueReportDto> GetRevenueAsync(
        Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var period = await ResolvePeriodAsync(organizationId, branchId, fromDate, toDate, cancellationToken);
        var (previousFrom, previousTo) = OrganizationAdminReportPeriod.PreviousOf(fromDate, toDate);
        var previousPeriod = await ResolvePeriodAsync(organizationId, branchId, previousFrom, previousTo, cancellationToken);
        var currentQuery = Query(period);
        var sales = await reports.GetSalesReportAsync(organizationId, branchId, currentQuery, cancellationToken);
        var gameplay = await reports.GetGameplayTimeReportAsync(organizationId, branchId, currentQuery, cancellationToken);
        var gameplayEntries = await GameplayMoneyAsync(organizationId, branchId, period, cancellationToken);
        var gameplayMoney = GameplayTotals.Of(gameplayEntries);
        var previousGameplayMoney = GameplayTotals.Of(await GameplayMoneyAsync(organizationId, branchId, previousPeriod, cancellationToken));
        var payments = await PosMoneyAsync(organizationId, branchId, period, cancellationToken);
        var posMoney = PosTotals.Of(payments);
        var previousPosMoney = PosTotals.Of(await PosMoneyAsync(organizationId, branchId, previousPeriod, cancellationToken));
        var net = posMoney.Net + gameplayMoney.Net;
        var previousNet = previousPosMoney.Net + previousGameplayMoney.Net;
        var difference = net - previousNet;
        decimal? percent = previousNet == 0 ? null : Math.Round(difference * 100m / Math.Abs(previousNet), 2);
        var currency = sales.NetSalesTotal.CurrencyCode;
        var staffIds = payments.Select(row => row.CreatedByStaffUserId).Concat(gameplayEntries.Select(row => row.CreatedByStaffUserId)).Distinct().ToList();
        var staffNames = await dbContext.StaffUsers.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && staffIds.Contains(row.StaffUserId))
            .ToDictionaryAsync(row => row.StaffUserId, row => row.DisplayName, cancellationToken);
        var paymentMethods = payments.GroupBy(row => row.PaymentMethod)
            .Select(group => new OrganizationAdminRevenueBreakdownDto(group.Key, group.Key, Money(currency, group.Sum(row => row.AmountMinorUnits))))
            .OrderByDescending(row => row.Revenue.MinorUnits).ToList();
        var operatorAmounts = payments.Select(row => (row.CreatedByStaffUserId, row.AmountMinorUnits))
            .Concat(gameplayEntries.Select(row => (row.CreatedByStaffUserId, GameplayImpact(row.EntryType, row.AmountMinorUnits))))
            .GroupBy(row => row.CreatedByStaffUserId)
            .Select(group => new OrganizationAdminRevenueBreakdownDto(group.Key.ToString("D"), staffNames.GetValueOrDefault(group.Key, "Staff"), Money(currency, group.Sum(row => row.Item2))))
            .OrderByDescending(row => row.Revenue.MinorUnits).ToList();

        return new OrganizationAdminRevenueReportDto(
            Dto(period),
            Money(currency, posMoney.Paid + gameplayMoney.Charged),
            Money(currency, posMoney.Refunded + gameplayMoney.Refunded),
            Money(currency, net),
            Money(currency, gameplayMoney.Net),
            gameplay.TotalDurationSeconds,
            Money(currency, posMoney.Net),
            new OrganizationAdminRevenueComparisonDto(Money(currency, previousNet), difference, percent),
            [new("gameplay", Money(currency, gameplayMoney.Net)), new("pos", Money(currency, posMoney.Net))],
            paymentMethods,
            operatorAmounts);
    }

    // Деньги — по дню, когда они прошли (решение владельца 2026-09-23). Одно правило на сводку,
    // «Выручку», тренд и разбивки по способам оплаты и кассирам. Раньше цифры брали продажи и
    // сессии, начатые в периоде, — возврат приписывался дню продажи, а возврат за игру не
    // вычитался вовсе, — тогда как тренд и разбивки считали движения денег по их дню. Неделя на
    // графике не сходилась с итогом той же недели. Время игры по-прежнему считается по сессиям:
    // это часы, а не деньги; список продаж — по-прежнему список продаж.
    //
    // Игра — строки журнала сессий: списание, долг по постоплате, возврат за игру.
    private Task<List<LedgerEntryEntity>> GameplayMoneyAsync(
        Guid organizationId, Guid branchId, OrganizationAdminReportPeriod period, CancellationToken cancellationToken) =>
        dbContext.LedgerEntries.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId &&
                row.SessionId != null &&
                row.CreatedAtUtc >= period.FromUtc && row.CreatedAtUtc < period.ToUtc &&
                (row.EntryType == LedgerEntryTypeNames.GameplayCharge || row.EntryType == LedgerEntryTypeNames.PostpaidDebt || row.EntryType == LedgerEntryTypeNames.Refund))
            .ToListAsync(cancellationToken);

    // Бар — оплаты по чекам: оплата в свой день, возврат (отрицательная сумма) в свой.
    private Task<List<PaymentEntity>> PosMoneyAsync(
        Guid organizationId, Guid branchId, OrganizationAdminReportPeriod period, CancellationToken cancellationToken) =>
        dbContext.Payments.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId && row.PosSaleId != null &&
                row.CreatedAtUtc >= period.FromUtc && row.CreatedAtUtc < period.ToUtc)
            .ToListAsync(cancellationToken);

    private readonly record struct PosTotals(long Paid, long Refunded)
    {
        public long Net => Paid + Refunded;

        public static PosTotals Of(IEnumerable<PaymentEntity> payments)
        {
            long paid = 0, refunded = 0;
            foreach (var payment in payments)
            {
                if (payment.PaymentKind == PaymentKindRefund) refunded += payment.AmountMinorUnits;
                else paid += payment.AmountMinorUnits;
            }
            return new(paid, refunded);
        }
    }

    private const string PaymentKindRefund = "refund";

    private readonly record struct GameplayTotals(long Charged, long Refunded)
    {
        public long Net => Charged + Refunded;

        public static GameplayTotals Of(IEnumerable<LedgerEntryEntity> entries)
        {
            long charged = 0, refunded = 0;
            foreach (var entry in entries)
            {
                var impact = GameplayImpact(entry.EntryType, entry.AmountMinorUnits);
                if (impact >= 0) charged += impact;
                else refunded += impact;
            }
            return new(charged, refunded);
        }
    }

    private async Task<OrganizationAdminReportPeriod> ResolvePeriodAsync(
        Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var timeZone = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && branch.BranchId == branchId)
            .Select(branch => branch.PreferredTimeZone)
            .SingleAsync(cancellationToken);
        return OrganizationAdminReportPeriod.Resolve(fromDate, toDate, timeZone);
    }

    private static ReportSearchQuery Query(OrganizationAdminReportPeriod period) =>
        new(period.FromUtc, period.ToUtc.AddTicks(-1), DetailLimit);

    private static OrganizationAdminReportPeriodDto Dto(OrganizationAdminReportPeriod period) =>
        new(period.FromDate, period.ToDate, period.TimeZone, period.FromUtc, period.ToUtc);

    private static MoneyDto Money(string currencyCode, long minorUnits) => new(currencyCode, minorUnits);

    private static long GameplayImpact(string entryType, long amountMinorUnits) => entryType switch
    {
        LedgerEntryTypeNames.GameplayCharge => Math.Abs(amountMinorUnits),
        LedgerEntryTypeNames.PostpaidDebt => Math.Max(0, amountMinorUnits),
        LedgerEntryTypeNames.Refund => -Math.Abs(amountMinorUnits),
        _ => 0
    };

    private static DateOnly LocalDate(DateTimeOffset timestamp, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, timeZone).DateTime);
}
