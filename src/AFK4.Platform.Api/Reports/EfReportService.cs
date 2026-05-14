using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reports;

public sealed class EfReportService(PlatformDbContext dbContext) : IReportService
{
    private const string DefaultCurrencyCode = "TJS";
    private const string PaymentKindPayment = "payment";
    private const string PaymentKindRefund = "refund";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public async Task<ShiftReportResultDto> GetShiftReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(query.Limit);
        var shiftsQuery = dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.OrganizationId == organizationId && shift.BranchId == branchId);

        if (query.FromUtc is not null)
        {
            var fromUtc = query.FromUtc.Value;
            shiftsQuery = shiftsQuery.Where(shift => shift.OpenedAtUtc >= fromUtc);
        }

        if (query.ToUtc is not null)
        {
            var toUtc = query.ToUtc.Value;
            shiftsQuery = shiftsQuery.Where(shift => shift.OpenedAtUtc <= toUtc);
        }

        var shifts = await shiftsQuery
            .OrderByDescending(shift => shift.OpenedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var shiftIds = shifts.Select(shift => shift.ShiftId).ToHashSet();

        var cashMovements = await dbContext.CashMovements
            .AsNoTracking()
            .Where(movement =>
                movement.OrganizationId == organizationId &&
                movement.BranchId == branchId &&
                shiftIds.Contains(movement.ShiftId))
            .ToListAsync(cancellationToken);
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.OrganizationId == organizationId &&
                payment.BranchId == branchId &&
                shiftIds.Contains(payment.ShiftId))
            .ToListAsync(cancellationToken);
        var ledgerEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                entry.ShiftId.HasValue &&
                shiftIds.Contains(entry.ShiftId.Value) &&
                (entry.EntryType == LedgerEntryTypeNames.TopUp ||
                 entry.EntryType == LedgerEntryTypeNames.DebtPayment ||
                 entry.EntryType == LedgerEntryTypeNames.ManualCorrection))
            .ToListAsync(cancellationToken);

        var rows = shifts.Select(shift =>
        {
            var currencyCode = shift.CurrencyCode;
            var cashMovementTotal = cashMovements
                .Where(movement => movement.ShiftId == shift.ShiftId && IsCurrency(movement.CurrencyCode, currencyCode))
                .Sum(movement => movement.MovementType == CashMovementTypeNames.CashIn
                    ? movement.AmountMinorUnits
                    : -movement.AmountMinorUnits);
            var posCashPaymentsTotal = payments
                .Where(payment =>
                    payment.ShiftId == shift.ShiftId &&
                    IsCurrency(payment.CurrencyCode, currencyCode) &&
                    payment.PaymentMethod == PaymentMethodNames.Cash &&
                    payment.PaymentKind == PaymentKindPayment)
                .Sum(payment => payment.AmountMinorUnits);
            var posRefundsTotal = payments
                .Where(payment =>
                    payment.ShiftId == shift.ShiftId &&
                    IsCurrency(payment.CurrencyCode, currencyCode) &&
                    payment.PaymentMethod == PaymentMethodNames.Cash &&
                    payment.PaymentKind == PaymentKindRefund)
                .Sum(payment => payment.AmountMinorUnits);
            var billingCashImpactTotal = ledgerEntries
                .Where(entry => entry.ShiftId == shift.ShiftId && IsCurrency(entry.CurrencyCode, currencyCode))
                .Sum(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment
                    ? -entry.AmountMinorUnits
                    : entry.AmountMinorUnits);
            var expectedCash = shift.StartingCashMinorUnits +
                cashMovementTotal +
                posCashPaymentsTotal +
                posRefundsTotal +
                billingCashImpactTotal;
            var isClosed = shift.State == ShiftStateNames.Closed;

            return new ShiftReportRowDto(
                shift.ShiftId,
                shift.OrganizationId,
                shift.BranchId,
                shift.OpenedByStaffUserId,
                shift.ClosedByStaffUserId,
                shift.State,
                Money(currencyCode, shift.StartingCashMinorUnits),
                Money(currencyCode, cashMovementTotal),
                Money(currencyCode, posCashPaymentsTotal),
                Money(currencyCode, posRefundsTotal),
                Money(currencyCode, billingCashImpactTotal),
                Money(currencyCode, expectedCash),
                isClosed ? Money(currencyCode, shift.CountedCashMinorUnits) : null,
                isClosed ? Money(currencyCode, shift.CountedCashMinorUnits - expectedCash) : null,
                shift.OpenedAtUtc,
                shift.ClosedAtUtc);
        }).ToList();

        return new ShiftReportResultDto(rows, limit);
    }

    public async Task<SalesReportResultDto> GetSalesReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(query.Limit);
        var salesQuery = dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.OrganizationId == organizationId && sale.BranchId == branchId);

        if (query.FromUtc is not null)
        {
            var fromUtc = query.FromUtc.Value;
            salesQuery = salesQuery.Where(sale => sale.CreatedAtUtc >= fromUtc);
        }

        if (query.ToUtc is not null)
        {
            var toUtc = query.ToUtc.Value;
            salesQuery = salesQuery.Where(sale => sale.CreatedAtUtc <= toUtc);
        }

        var sales = await salesQuery
            .OrderByDescending(sale => sale.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var saleIds = sales.Select(sale => sale.PosSaleId).ToHashSet();
        var lines = await dbContext.PosSaleLines
            .AsNoTracking()
            .Where(line => saleIds.Contains(line.PosSaleId))
            .ToListAsync(cancellationToken);
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.OrganizationId == organizationId &&
                payment.BranchId == branchId &&
                saleIds.Contains(payment.PosSaleId))
            .ToListAsync(cancellationToken);

        var rows = sales.Select(sale =>
        {
            var currencyCode = sale.CurrencyCode;
            var salePayments = payments
                .Where(payment => payment.PosSaleId == sale.PosSaleId && IsCurrency(payment.CurrencyCode, currencyCode))
                .ToList();
            var paidAmount = salePayments
                .Where(payment => payment.PaymentKind == PaymentKindPayment)
                .Sum(payment => payment.AmountMinorUnits);
            var refundAmount = salePayments
                .Where(payment => payment.PaymentKind == PaymentKindRefund)
                .Sum(payment => payment.AmountMinorUnits);
            var saleLines = lines.Where(line => line.PosSaleId == sale.PosSaleId).ToList();

            return new SalesReportRowDto(
                sale.PosSaleId,
                sale.OrganizationId,
                sale.BranchId,
                sale.ShiftId,
                sale.CreatedByStaffUserId,
                sale.State,
                Money(currencyCode, sale.TotalMinorUnits),
                Money(currencyCode, paidAmount),
                Money(currencyCode, refundAmount),
                saleLines.Count,
                saleLines.Sum(line => line.Quantity),
                sale.CreatedAtUtc,
                sale.PaidAtUtc,
                sale.RefundedAtUtc,
                sale.VoidedAtUtc);
        }).ToList();

        var resultCurrencyCode = rows.FirstOrDefault()?.Total.CurrencyCode ?? DefaultCurrencyCode;
        var grossSalesTotal = rows.Sum(row => row.PaidAmount.MinorUnits);
        var refundsTotal = rows.Sum(row => row.RefundAmount.MinorUnits);

        return new SalesReportResultDto(
            rows,
            limit,
            Money(resultCurrencyCode, grossSalesTotal),
            Money(resultCurrencyCode, refundsTotal),
            Money(resultCurrencyCode, grossSalesTotal + refundsTotal));
    }

    private static int NormalizeLimit(int? limit)
    {
        return limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
    }

    private static bool IsCurrency(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static MoneyDto Money(string currencyCode, long minorUnits)
    {
        return new MoneyDto(currencyCode, minorUnits);
    }
}
