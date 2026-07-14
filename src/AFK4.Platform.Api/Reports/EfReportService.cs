using AFK4.Platform.Api.Audit;
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

        var reportCurrencies = (await salesQuery
                .Select(sale => sale.CurrencyCode)
                .Distinct()
                .ToListAsync(cancellationToken))
            .Select(NormalizeCurrency)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(currency => currency, StringComparer.Ordinal)
            .ToList();
        if (reportCurrencies.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Sales report contains a sale with an empty currency code.");
        }

        if (reportCurrencies.Count > 1)
        {
            throw new InvalidOperationException(
                $"Sales report range contains multiple currencies: {string.Join(", ", reportCurrencies)}.");
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
                payment.PosSaleId != null &&
                saleIds.Contains(payment.PosSaleId.Value))
            .ToListAsync(cancellationToken);

        foreach (var sale in sales)
        {
            if (lines.Any(line =>
                    line.PosSaleId == sale.PosSaleId &&
                    !IsCurrency(line.CurrencyCode, sale.CurrencyCode)))
            {
                throw new InvalidOperationException(
                    $"Sales report line currency does not match sale {sale.PosSaleId:D} currency.");
            }

            if (payments.Any(payment =>
                    payment.PosSaleId == sale.PosSaleId &&
                    !IsCurrency(payment.CurrencyCode, sale.CurrencyCode)))
            {
                throw new InvalidOperationException(
                    $"Sales report payment currency does not match sale {sale.PosSaleId:D} currency.");
            }
        }

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
            var grossCostMinorUnits = sale.State is PosSaleStateNames.Paid or PosSaleStateNames.Refunded
                ? saleLines.Aggregate(
                    0L,
                    (total, line) => checked(total + checked((long)line.Quantity * line.UnitCostMinorUnits)))
                : 0;
            var refundedCostMinorUnits = sale.State == PosSaleStateNames.Refunded
                ? checked(-grossCostMinorUnits)
                : 0;
            var netCostMinorUnits = checked(grossCostMinorUnits + refundedCostMinorUnits);

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
                sale.VoidedAtUtc,
                Money(currencyCode, grossCostMinorUnits),
                Money(currencyCode, refundedCostMinorUnits),
                Money(currencyCode, netCostMinorUnits));
        }).ToList();

        var resultCurrencyCode = rows.FirstOrDefault()?.Total.CurrencyCode ?? DefaultCurrencyCode;
        var grossSalesTotal = CheckedSum(rows.Select(row => row.PaidAmount.MinorUnits));
        var refundsTotal = CheckedSum(rows.Select(row => row.RefundAmount.MinorUnits));
        var grossCostOfGoodsTotal = CheckedSum(rows.Select(row => row.GrossCostOfGoods.MinorUnits));
        var refundedCostOfGoodsTotal = CheckedSum(rows.Select(row => row.RefundedCostOfGoods.MinorUnits));
        var netCostOfGoodsTotal = CheckedSum(rows.Select(row => row.NetCostOfGoods.MinorUnits));

        return new SalesReportResultDto(
            rows,
            limit,
            Money(resultCurrencyCode, grossSalesTotal),
            Money(resultCurrencyCode, refundsTotal),
            Money(resultCurrencyCode, checked(grossSalesTotal + refundsTotal)),
            Money(resultCurrencyCode, grossCostOfGoodsTotal),
            Money(resultCurrencyCode, refundedCostOfGoodsTotal),
            Money(resultCurrencyCode, netCostOfGoodsTotal));
    }

    public async Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(query.Limit);
        var sessionsQuery = dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.OrganizationId == organizationId &&
                session.BranchId == branchId &&
                session.StartedAtUtc.HasValue);

        if (query.FromUtc is not null)
        {
            var fromUtc = query.FromUtc.Value;
            sessionsQuery = sessionsQuery.Where(session => session.StartedAtUtc >= fromUtc);
        }

        if (query.ToUtc is not null)
        {
            var toUtc = query.ToUtc.Value;
            sessionsQuery = sessionsQuery.Where(session => session.StartedAtUtc <= toUtc);
        }

        var sessions = await sessionsQuery
            .OrderByDescending(session => session.StartedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(session => session.SessionId).ToHashSet();
        var ledgerEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                entry.SessionId.HasValue &&
                sessionIds.Contains(entry.SessionId.Value) &&
                (entry.EntryType == LedgerEntryTypeNames.GameplayCharge ||
                 entry.EntryType == LedgerEntryTypeNames.PostpaidDebt ||
                 entry.EntryType == LedgerEntryTypeNames.PackageConsumption ||
                 entry.EntryType == LedgerEntryTypeNames.BonusConsumption))
            .ToListAsync(cancellationToken);

        var rows = sessions.Select(session =>
        {
            var entries = ledgerEntries
                .Where(entry => entry.SessionId == session.SessionId)
                .ToList();
            var currencyCode = entries.FirstOrDefault()?.CurrencyCode ?? DefaultCurrencyCode;
            var gameplayRevenue = entries.Sum(entry => entry.EntryType switch
            {
                LedgerEntryTypeNames.GameplayCharge => Math.Abs(entry.AmountMinorUnits),
                LedgerEntryTypeNames.PostpaidDebt => entry.AmountMinorUnits,
                _ => 0
            });
            var packageSeconds = entries
                .Where(entry => entry.EntryType == LedgerEntryTypeNames.PackageConsumption)
                .Sum(entry => Math.Abs(entry.QuantitySeconds));
            var bonusSeconds = entries
                .Where(entry => entry.EntryType == LedgerEntryTypeNames.BonusConsumption)
                .Sum(entry => Math.Abs(entry.QuantitySeconds));

            return new GameplayTimeReportRowDto(
                session.SessionId,
                session.OrganizationId,
                session.BranchId,
                session.SeatId,
                session.DeviceId,
                session.CreatedByStaffUserId,
                session.PlayerKind,
                session.PlayerAccountId,
                session.State,
                CalculateDurationSeconds(session.StartedAtUtc, session.EndedAtUtc, session.EndsAtUtc),
                packageSeconds,
                bonusSeconds,
                Money(currencyCode, gameplayRevenue),
                session.StartedAtUtc,
                session.EndedAtUtc,
                session.EndsAtUtc);
        }).ToList();

        var resultCurrencyCode = rows.FirstOrDefault()?.GameplayRevenue.CurrencyCode ?? DefaultCurrencyCode;

        return new GameplayTimeReportResultDto(
            rows,
            limit,
            rows.Sum(row => row.DurationSeconds),
            rows.Sum(row => row.PackageSeconds),
            rows.Sum(row => row.BonusSeconds),
            Money(resultCurrencyCode, rows.Sum(row => row.GameplayRevenue.MinorUnits)));
    }

    public async Task<CashOperationReportResultDto> GetCashOperationReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(query.Limit);
        var cashMovementsQuery = dbContext.CashMovements
            .AsNoTracking()
            .Where(movement => movement.OrganizationId == organizationId && movement.BranchId == branchId);
        var paymentsQuery = dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.OrganizationId == organizationId &&
                payment.BranchId == branchId &&
                payment.PaymentMethod == PaymentMethodNames.Cash);
        var ledgerEntriesQuery = dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                entry.ShiftId.HasValue &&
                (entry.EntryType == LedgerEntryTypeNames.TopUp ||
                 entry.EntryType == LedgerEntryTypeNames.DebtPayment ||
                 entry.EntryType == LedgerEntryTypeNames.ManualCorrection ||
                 entry.EntryType == LedgerEntryTypeNames.Refund));

        if (query.FromUtc is not null)
        {
            var fromUtc = query.FromUtc.Value;
            cashMovementsQuery = cashMovementsQuery.Where(movement => movement.CreatedAtUtc >= fromUtc);
            paymentsQuery = paymentsQuery.Where(payment => payment.CreatedAtUtc >= fromUtc);
            ledgerEntriesQuery = ledgerEntriesQuery.Where(entry => entry.CreatedAtUtc >= fromUtc);
        }

        if (query.ToUtc is not null)
        {
            var toUtc = query.ToUtc.Value;
            cashMovementsQuery = cashMovementsQuery.Where(movement => movement.CreatedAtUtc <= toUtc);
            paymentsQuery = paymentsQuery.Where(payment => payment.CreatedAtUtc <= toUtc);
            ledgerEntriesQuery = ledgerEntriesQuery.Where(entry => entry.CreatedAtUtc <= toUtc);
        }

        var cashMovements = await cashMovementsQuery.ToListAsync(cancellationToken);
        var payments = await paymentsQuery.ToListAsync(cancellationToken);
        var ledgerEntries = await ledgerEntriesQuery.ToListAsync(cancellationToken);
        var rows = cashMovements
            .Select(movement => new CashOperationReportRowDto(
                movement.CashMovementId,
                movement.OrganizationId,
                movement.BranchId,
                movement.ShiftId,
                movement.CreatedByStaffUserId,
                "cash_movement",
                movement.MovementType,
                Money(movement.CurrencyCode, movement.MovementType == CashMovementTypeNames.CashIn
                    ? movement.AmountMinorUnits
                    : -movement.AmountMinorUnits),
                movement.Reason,
                movement.CreatedAtUtc))
            .Concat(payments.Select(payment => new CashOperationReportRowDto(
                payment.PaymentId,
                payment.OrganizationId,
                payment.BranchId,
                payment.ShiftId,
                payment.CreatedByStaffUserId,
                "pos_payment",
                payment.PaymentKind,
                Money(payment.CurrencyCode, payment.AmountMinorUnits),
                payment.Note,
                payment.CreatedAtUtc)))
            .Concat(ledgerEntries.Select(entry => new CashOperationReportRowDto(
                entry.LedgerEntryId,
                entry.OrganizationId,
                entry.BranchId,
                entry.ShiftId,
                entry.CreatedByStaffUserId,
                "ledger_entry",
                entry.EntryType,
                Money(entry.CurrencyCode, entry.EntryType == LedgerEntryTypeNames.DebtPayment
                    ? -entry.AmountMinorUnits
                    : entry.AmountMinorUnits),
                entry.Reason,
                entry.CreatedAtUtc)))
            .OrderByDescending(row => row.CreatedAtUtc)
            .ThenByDescending(row => row.OperationId)
            .Take(limit)
            .ToList();
        var resultCurrencyCode = rows.FirstOrDefault()?.CashImpact.CurrencyCode ?? DefaultCurrencyCode;
        var cashInTotal = rows
            .Where(row => row.CashImpact.MinorUnits > 0)
            .Sum(row => row.CashImpact.MinorUnits);
        var cashOutTotal = rows
            .Where(row => row.CashImpact.MinorUnits < 0)
            .Sum(row => row.CashImpact.MinorUnits);

        return new CashOperationReportResultDto(
            rows,
            limit,
            Money(resultCurrencyCode, cashInTotal),
            Money(resultCurrencyCode, cashOutTotal),
            Money(resultCurrencyCode, cashInTotal + cashOutTotal));
    }

    public async Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(query.Limit);
        var auditQuery = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId && record.BranchId == branchId);

        if (query.FromUtc is not null)
        {
            var fromUtc = query.FromUtc.Value;
            auditQuery = auditQuery.Where(record => record.CreatedAtUtc >= fromUtc);
        }

        if (query.ToUtc is not null)
        {
            var toUtc = query.ToUtc.Value;
            auditQuery = auditQuery.Where(record => record.CreatedAtUtc <= toUtc);
        }

        if (query.ActorStaffUserId is not null)
        {
            var actorStaffUserId = query.ActorStaffUserId.Value;
            auditQuery = auditQuery.Where(record => record.ActorStaffUserId == actorStaffUserId);
        }

        // Amount bounds only match money-relevant records (those carrying an amount); amountless rows
        // are excluded when a bound is set.
        if (query.MinAmountMinorUnits is not null)
        {
            var minAmount = query.MinAmountMinorUnits.Value;
            auditQuery = auditQuery.Where(record =>
                record.AmountMinorUnits != null && record.AmountMinorUnits >= minAmount);
        }

        if (query.MaxAmountMinorUnits is not null)
        {
            var maxAmount = query.MaxAmountMinorUnits.Value;
            auditQuery = auditQuery.Where(record =>
                record.AmountMinorUnits != null && record.AmountMinorUnits <= maxAmount);
        }

        var auditRecords = await auditQuery.ToListAsync(cancellationToken);
        var actorIds = auditRecords
            .Where(record => record.ActorStaffUserId.HasValue)
            .Select(record => record.ActorStaffUserId!.Value)
            .Distinct()
            .ToList();
        var actorNames = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(user => user.OrganizationId == organizationId && actorIds.Contains(user.StaffUserId))
            .ToDictionaryAsync(user => user.StaffUserId, user => user.DisplayName, cancellationToken);
        var rows = auditRecords
            .GroupBy(record => new
            {
                record.ActorStaffUserId,
                record.Action,
                record.Outcome
            })
            .Select(group => new OperatorActionReportRowDto(
                group.Key.ActorStaffUserId,
                GetActorDisplayName(group.Key.ActorStaffUserId, actorNames),
                group.Key.Action,
                group.Key.Outcome,
                group.Count(),
                group.Min(record => record.CreatedAtUtc),
                group.Max(record => record.CreatedAtUtc)))
            .OrderByDescending(row => row.LastAtUtc)
            .ThenBy(row => row.ActorDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Action, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

        return new OperatorActionReportResultDto(rows, limit, auditRecords.Count);
    }

    public async Task<OwnerDailySummaryResultDto> GetOwnerDailySummaryAsync(
        Guid organizationId,
        Guid branchId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var windowStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(1);

        var moneyEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OrganizationId == organizationId
                && entry.BranchId == branchId
                && entry.CreatedAtUtc >= windowStart && entry.CreatedAtUtc < windowEnd
                && (entry.EntryType == LedgerEntryTypeNames.Refund
                    || entry.EntryType == LedgerEntryTypeNames.Reversal
                    || entry.EntryType == LedgerEntryTypeNames.ManualCorrection))
            .ToListAsync(cancellationToken);

        var compAudits = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId
                && record.BranchId == branchId
                && record.Action == AuditActionNames.SessionComp
                && record.CreatedAtUtc >= windowStart && record.CreatedAtUtc < windowEnd)
            .ToListAsync(cancellationToken);

        var closedShifts = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.OrganizationId == organizationId
                && shift.BranchId == branchId
                && shift.State == ShiftStateNames.Closed
                && shift.ClosedAtUtc >= windowStart && shift.ClosedAtUtc < windowEnd)
            .ToListAsync(cancellationToken);

        var actorIds = moneyEntries.Select(entry => entry.CreatedByStaffUserId)
            .Concat(compAudits.Where(record => record.ActorStaffUserId.HasValue)
                .Select(record => record.ActorStaffUserId!.Value))
            .Concat(closedShifts.Where(shift => shift.ClosedByStaffUserId.HasValue)
                .Select(shift => shift.ClosedByStaffUserId!.Value))
            .Distinct()
            .ToList();
        var actorNames = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(user => user.OrganizationId == organizationId && actorIds.Contains(user.StaffUserId))
            .ToDictionaryAsync(user => user.StaffUserId, user => user.DisplayName, cancellationToken);

        var currencyCode = moneyEntries.FirstOrDefault()?.CurrencyCode
            ?? closedShifts.FirstOrDefault()?.CurrencyCode
            ?? DefaultCurrencyCode;

        return OwnerDailySummaryAggregator.Build(
            date, currencyCode, moneyEntries, compAudits, closedShifts, actorNames);
    }

    public async Task<ShiftRevenueDto?> GetCurrentShiftRevenueAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var shift = await dbContext.Shifts
            .AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.BranchId == branchId && s.State == ShiftStateNames.Open)
            .OrderByDescending(s => s.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (shift is null)
        {
            return null;
        }

        var data = await LoadShiftRevenueSourcesAsync(organizationId, branchId, new HashSet<Guid> { shift.ShiftId }, cancellationToken);
        return BuildShiftRevenue(shift, data);
    }

    public async Task<ShiftRevenueListDto> GetShiftRevenueAsync(
        Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(query.Limit);
        var shiftsQuery = dbContext.Shifts
            .AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.BranchId == branchId);

        if (query.FromUtc is not null)
        {
            var fromUtc = query.FromUtc.Value;
            shiftsQuery = shiftsQuery.Where(s => s.OpenedAtUtc >= fromUtc);
        }

        if (query.ToUtc is not null)
        {
            var toUtc = query.ToUtc.Value;
            shiftsQuery = shiftsQuery.Where(s => s.OpenedAtUtc <= toUtc);
        }

        var shifts = await shiftsQuery
            .OrderByDescending(s => s.OpenedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var data = await LoadShiftRevenueSourcesAsync(
            organizationId, branchId, shifts.Select(s => s.ShiftId).ToHashSet(), cancellationToken);

        var rows = shifts.Select(s => BuildShiftRevenue(s, data)).ToList();
        return new ShiftRevenueListDto(rows, limit);
    }

    // Batch-loads every money source for the given shift ids in one query each (mirrors GetShiftReportAsync),
    // avoiding an N+1 when building a shift history.
    private async Task<ShiftRevenueSources> LoadShiftRevenueSourcesAsync(
        Guid organizationId, Guid branchId, IReadOnlyCollection<Guid> shiftIds, CancellationToken cancellationToken)
    {
        var ledger = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.BranchId == branchId && e.ShiftId != null && shiftIds.Contains(e.ShiftId.Value))
            .ToListAsync(cancellationToken);
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.BranchId == branchId && shiftIds.Contains(p.ShiftId))
            .ToListAsync(cancellationToken);
        var sales = await dbContext.PosSales
            .AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.BranchId == branchId && shiftIds.Contains(s.ShiftId))
            .ToListAsync(cancellationToken);
        var cashMovements = await dbContext.CashMovements
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.BranchId == branchId && shiftIds.Contains(m.ShiftId))
            .ToListAsync(cancellationToken);
        return new ShiftRevenueSources(ledger, payments, sales, cashMovements);
    }

    private sealed record ShiftRevenueSources(
        IReadOnlyList<LedgerEntryEntity> Ledger,
        IReadOnlyList<PaymentEntity> Payments,
        IReadOnlyList<PosSaleEntity> Sales,
        IReadOnlyList<CashMovementEntity> CashMovements);

    private static ShiftRevenueDto BuildShiftRevenue(ShiftEntity shift, ShiftRevenueSources data)
    {
        var currency = shift.CurrencyCode;
        bool Cur(string code) => IsCurrency(code, currency);

        // Refund payments are stored with a NEGATIVE AmountMinorUnits (see EfPosService.RefundSaleAsync),
        // so summing payment+refund amounts as-is yields the correct net; do not negate refunds.
        var ledger = data.Ledger.Where(e => e.ShiftId == shift.ShiftId).ToList();
        var payments = data.Payments.Where(p => p.ShiftId == shift.ShiftId).ToList();
        var sales = data.Sales.Where(s => s.ShiftId == shift.ShiftId).ToList();
        var cashMovements = data.CashMovements.Where(m => m.ShiftId == shift.ShiftId).ToList();

        var earnedTime =
            -ledger.Where(e => e.EntryType == LedgerEntryTypeNames.GameplayCharge && Cur(e.CurrencyCode))
                   .Sum(e => e.AmountMinorUnits)
            + ledger.Where(e => e.EntryType == LedgerEntryTypeNames.PostpaidDebt && Cur(e.CurrencyCode))
                    .Sum(e => e.AmountMinorUnits);
        var earnedGoods = sales
            .Where(s => Cur(s.CurrencyCode) && s.PaidAtUtc != null && s.VoidedAtUtc == null && s.RefundedAtUtc == null)
            .Sum(s => s.TotalMinorUnits);

        long MethodNet(string method) =>
            payments
                .Where(p => p.PaymentMethod == method && Cur(p.CurrencyCode)
                    && (p.PaymentKind == PaymentKindPayment || p.PaymentKind == PaymentKindRefund))
                .Sum(p => p.AmountMinorUnits);

        var cash = MethodNet(PaymentMethodNames.Cash);
        var nonCash = MethodNet(PaymentMethodNames.CardManual);
        var walletTopUps = ledger
            .Where(e => e.EntryType == LedgerEntryTypeNames.TopUp && Cur(e.CurrencyCode))
            .Sum(e => e.AmountMinorUnits);

        var cashMovementTotal = cashMovements
            .Where(m => Cur(m.CurrencyCode))
            .Sum(m => m.MovementType == CashMovementTypeNames.CashIn ? m.AmountMinorUnits : -m.AmountMinorUnits);
        var posCashPayments = payments
            .Where(p => p.PaymentMethod == PaymentMethodNames.Cash && Cur(p.CurrencyCode) && p.PaymentKind == PaymentKindPayment)
            .Sum(p => p.AmountMinorUnits);
        var posCashRefunds = payments
            .Where(p => p.PaymentMethod == PaymentMethodNames.Cash && Cur(p.CurrencyCode) && p.PaymentKind == PaymentKindRefund)
            .Sum(p => p.AmountMinorUnits);
        var billingCashImpact = ledger
            .Where(e => Cur(e.CurrencyCode) &&
                (e.EntryType == LedgerEntryTypeNames.TopUp ||
                 e.EntryType == LedgerEntryTypeNames.DebtPayment ||
                 e.EntryType == LedgerEntryTypeNames.ManualCorrection))
            .Sum(e => e.EntryType == LedgerEntryTypeNames.DebtPayment ? -e.AmountMinorUnits : e.AmountMinorUnits);
        var expectedCash = shift.StartingCashMinorUnits + cashMovementTotal + posCashPayments + posCashRefunds + billingCashImpact;
        var isClosed = shift.State == ShiftStateNames.Closed;

        return new ShiftRevenueDto(
            shift.ShiftId,
            shift.OrganizationId,
            shift.BranchId,
            shift.OpenedByStaffUserId,
            shift.ClosedByStaffUserId,
            shift.State,
            new EarnedBreakdownDto(Money(currency, earnedTime), Money(currency, earnedGoods), Money(currency, earnedTime + earnedGoods)),
            new InflowBreakdownDto(Money(currency, cash), Money(currency, nonCash), Money(currency, walletTopUps), Money(currency, cash + nonCash)),
            new CashReconciliationDto(
                Money(currency, shift.StartingCashMinorUnits),
                Money(currency, expectedCash),
                isClosed ? Money(currency, shift.CountedCashMinorUnits) : null,
                isClosed ? Money(currency, shift.CountedCashMinorUnits - expectedCash) : null),
            shift.OpenedAtUtc,
            shift.ClosedAtUtc);
    }

    private static int NormalizeLimit(int? limit)
    {
        return limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
    }

    private static bool IsCurrency(string actual, string expected)
    {
        return string.Equals(
            NormalizeCurrency(actual),
            NormalizeCurrency(expected),
            StringComparison.Ordinal);
    }

    private static string NormalizeCurrency(string currencyCode) =>
        currencyCode.Trim().ToUpperInvariant();

    private static long CheckedSum(IEnumerable<long> values)
    {
        return values.Aggregate(0L, (total, value) => checked(total + value));
    }

    private static int CalculateDurationSeconds(
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? endedAtUtc,
        DateTimeOffset? endsAtUtc)
    {
        if (startedAtUtc is null)
        {
            return 0;
        }

        var effectiveEnd = endedAtUtc ?? endsAtUtc;
        if (effectiveEnd is null || effectiveEnd <= startedAtUtc)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, Math.Floor((effectiveEnd.Value - startedAtUtc.Value).TotalSeconds));
    }

    private static string GetActorDisplayName(
        Guid? actorStaffUserId,
        IReadOnlyDictionary<Guid, string> actorNames)
    {
        if (actorStaffUserId is null)
        {
            return "System";
        }

        if (SystemActorIds.TryGetDisplayName(actorStaffUserId.Value, out var systemDisplayName))
        {
            return systemDisplayName;
        }

        return actorNames.TryGetValue(actorStaffUserId.Value, out var displayName) &&
            !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : actorStaffUserId.Value.ToString("N")[..8];
    }

    private static MoneyDto Money(string currencyCode, long minorUnits)
    {
        return new MoneyDto(currencyCode, minorUnits);
    }
}
