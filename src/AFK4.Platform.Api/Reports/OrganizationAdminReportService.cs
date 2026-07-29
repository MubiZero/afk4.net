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
        var trendPayments = await dbContext.Payments.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId && row.PosSaleId != null &&
                row.CreatedAtUtc >= trendPeriod.FromUtc && row.CreatedAtUtc < trendPeriod.ToUtc)
            .ToListAsync(cancellationToken);
        var trendGameplay = await dbContext.LedgerEntries.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId &&
                row.SessionId != null &&
                row.CreatedAtUtc >= trendPeriod.FromUtc && row.CreatedAtUtc < trendPeriod.ToUtc &&
                (row.EntryType == LedgerEntryTypeNames.GameplayCharge || row.EntryType == LedgerEntryTypeNames.PostpaidDebt || row.EntryType == LedgerEntryTypeNames.Refund))
            .ToListAsync(cancellationToken);
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
                Money(sales.NetSalesTotal.CurrencyCode, sales.NetSalesTotal.MinorUnits + gameplay.GameplayRevenueTotal.MinorUnits),
                gameplay.GameplayRevenueTotal, sales.NetSalesTotal, gameplay.TotalDurationSeconds),
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
        var dayCount = toDate.DayNumber - fromDate.DayNumber + 1;
        var previousTo = fromDate.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(dayCount - 1));
        var previousPeriod = await ResolvePeriodAsync(organizationId, branchId, previousFrom, previousTo, cancellationToken);
        var currentQuery = Query(period);
        var previousQuery = Query(previousPeriod);
        var sales = await reports.GetSalesReportAsync(organizationId, branchId, currentQuery, cancellationToken);
        var gameplay = await reports.GetGameplayTimeReportAsync(organizationId, branchId, currentQuery, cancellationToken);
        var previousSales = await reports.GetSalesReportAsync(organizationId, branchId, previousQuery, cancellationToken);
        var previousGameplay = await reports.GetGameplayTimeReportAsync(organizationId, branchId, previousQuery, cancellationToken);
        var net = sales.NetSalesTotal.MinorUnits + gameplay.GameplayRevenueTotal.MinorUnits;
        var previousNet = previousSales.NetSalesTotal.MinorUnits + previousGameplay.GameplayRevenueTotal.MinorUnits;
        var difference = net - previousNet;
        decimal? percent = previousNet == 0 ? null : Math.Round(difference * 100m / Math.Abs(previousNet), 2);
        var currency = sales.NetSalesTotal.CurrencyCode;
        var payments = await dbContext.Payments.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId && row.PosSaleId != null &&
                row.CreatedAtUtc >= period.FromUtc && row.CreatedAtUtc < period.ToUtc)
            .ToListAsync(cancellationToken);
        var gameplayEntries = await dbContext.LedgerEntries.AsNoTracking()
            .Where(row => row.OrganizationId == organizationId && row.BranchId == branchId &&
                row.SessionId != null &&
                row.CreatedAtUtc >= period.FromUtc && row.CreatedAtUtc < period.ToUtc &&
                (row.EntryType == LedgerEntryTypeNames.GameplayCharge || row.EntryType == LedgerEntryTypeNames.PostpaidDebt || row.EntryType == LedgerEntryTypeNames.Refund))
            .ToListAsync(cancellationToken);
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
            Money(currency, sales.GrossSalesTotal.MinorUnits + gameplay.GameplayRevenueTotal.MinorUnits),
            sales.RefundsTotal,
            Money(currency, net),
            gameplay.GameplayRevenueTotal,
            gameplay.TotalDurationSeconds,
            sales.NetSalesTotal,
            new OrganizationAdminRevenueComparisonDto(Money(currency, previousNet), difference, percent),
            [new("gameplay", gameplay.GameplayRevenueTotal), new("pos", sales.NetSalesTotal)],
            paymentMethods,
            operatorAmounts);
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
