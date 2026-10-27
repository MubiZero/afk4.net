using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Касса из журнала без связи: продажа товара за наличные, закрытие и открытие смены
/// (docs/roadmap/offline-front-desk.md). Как и с сессиями — те же строки, что онлайн, но время, смена и
/// деньги из журнала. Склад и подпись управляющего здесь не останавливают: товар уже отдан, смену уже
/// сдали — сервер записывает, что было, а расхождение видит владелец.
/// </summary>
public sealed class OfflineCashRecords(
    PlatformDbContext db,
    IReceiptNumberGenerator receiptNumbers,
    EfShiftDiscrepancyNotifier? discrepancyNotifier = null)
{
    private const int MaxLines = 100;
    private const int MaxQuantity = 1000;

    /// <summary>
    /// Пометка смены, закрытой без связи с расхождением сверх допуска: подписать её было некому. Владелец
    /// получает обычную тревогу о расхождении, а отчёт смены показывает, что подписи нет.
    /// </summary>
    public const string UnsignedOfflineCloseReason = "offline: closed without connection, no manager sign-off";

    public async Task<string?> SaleAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, IReadOnlySet<string> granted, CancellationToken cancellationToken)
    {
        if (!granted.Contains(OrganizationPermissionNames.CreatePosSale) || !granted.Contains(OrganizationPermissionNames.PayPosSale))
        {
            return OfflineJournalRejectionNames.NotAllowed;
        }

        if (record.Lines is not { Count: > 0 and <= MaxLines } lines
            || lines.Any(line => line.Quantity is < 1 or > MaxQuantity || line.UnitPriceMinorUnits < 0)
            || string.IsNullOrWhiteSpace(record.CurrencyCode)
            || record.ChargeMinorUnits != lines.Sum(line => line.UnitPriceMinorUnits * line.Quantity))
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        var shift = await OpenShiftAsync(organizationId, branchId, record.ShiftId, cancellationToken);
        if (shift.Refusal is not null)
        {
            return shift.Refusal;
        }

        var currencyCode = record.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(shift.Entity!.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        // Товар ищем и среди снятых с продажи: его продали по кэшу, пока сервер был недоступен, — деньги взяты.
        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var products = await db.PosProducts.AsNoTracking()
            .Where(product => product.OrganizationId == organizationId && product.BranchId == branchId && productIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);
        if (products.Count != productIds.Count)
        {
            return OfflineJournalRejectionNames.UnknownTarget;
        }

        var sale = new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ShiftId = record.ShiftId,
            CreatedByStaffUserId = staffUserId,
            State = PosSaleStateNames.Paid,
            CurrencyCode = currencyCode,
            TotalMinorUnits = record.ChargeMinorUnits,
            CreatedAtUtc = record.OccurredAtUtc,
            PaidAtUtc = record.OccurredAtUtc
        };
        db.PosSales.Add(sale);

        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            db.PosSaleLines.Add(new PosSaleLineEntity
            {
                PosSaleLineId = Guid.NewGuid(),
                PosSaleId = sale.PosSaleId,
                ProductId = product.ProductId,
                ProductName = product.Name,
                Quantity = line.Quantity,
                CurrencyCode = currencyCode,
                UnitPriceMinorUnits = line.UnitPriceMinorUnits,
                UnitCostMinorUnits = product.TrackStock ? product.AvgCostMinorUnits : 0,
                LineTotalMinorUnits = line.UnitPriceMinorUnits * line.Quantity,
                TracksStock = product.TrackStock,
                AllowNegativeStock = product.AllowNegativeStock
            });

            if (product.TrackStock)
            {
                db.StockMovements.Add(new StockMovementEntity
                {
                    StockMovementId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    ProductId = product.ProductId,
                    MovementType = StockMovementTypeNames.Sale,
                    QuantityDelta = -line.Quantity,
                    CurrencyCode = currencyCode,
                    UnitCostMinorUnits = product.AvgCostMinorUnits,
                    Reason = $"POS sale {sale.PosSaleId:D} (offline)",
                    CreatedByStaffUserId = staffUserId,
                    CreatedAtUtc = record.OccurredAtUtc
                });
            }
        }

        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PosSaleId = sale.PosSaleId,
            ShiftId = record.ShiftId,
            CreatedByStaffUserId = staffUserId,
            PaymentKind = "payment",
            Provider = "manual",
            PaymentMethod = PaymentMethodNames.Cash,
            CurrencyCode = currencyCode,
            AmountMinorUnits = record.ChargeMinorUnits,
            Note = "offline",
            CreatedAtUtc = record.OccurredAtUtc
        });

        db.Receipts.Add(new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PosSaleId = sale.PosSaleId,
            ReceiptNumber = await receiptNumbers.GenerateAsync(organizationId, branchId, "sale", record.OccurredAtUtc, cancellationToken),
            ReceiptType = "sale",
            CurrencyCode = currencyCode,
            TotalMinorUnits = record.ChargeMinorUnits,
            Locale = await db.Branches.Where(branch => branch.BranchId == branchId).Select(branch => branch.PreferredLocale).FirstOrDefaultAsync(cancellationToken) ?? "ru",
            CreatedAtUtc = record.OccurredAtUtc
        });
        return null;
    }

    /// <summary>
    /// Смену сдали без связи, пересчитав ящик вслепую: ожидаемое и расхождение сервер считает сам, по всем
    /// записям смены, включая присланные раньше в том же журнале.
    /// </summary>
    public async Task<string?> CloseShiftAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, IReadOnlySet<string> granted, CancellationToken cancellationToken)
    {
        if (record.CashMinorUnits < 0)
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        var found = await OpenShiftAsync(organizationId, branchId, record.ShiftId, cancellationToken, track: true);
        if (found.Refusal is not null)
        {
            return found.Refusal;
        }

        var shift = found.Entity!;
        var mayClose = granted.Contains(OrganizationPermissionNames.CloseShift)
            || (granted.Contains(OrganizationPermissionNames.CloseOwnShift) && shift.OpenedByStaffUserId == staffUserId);
        if (!mayClose)
        {
            return OfflineJournalRejectionNames.NotAllowed;
        }

        var expected = ShiftExpectedCash.Compute(
            shift,
            await db.CashMovements.Where(movement => movement.ShiftId == shift.ShiftId).ToListAsync(cancellationToken),
            await db.Payments.Where(payment => payment.ShiftId == shift.ShiftId).ToListAsync(cancellationToken),
            await db.LedgerEntries.Where(entry => entry.ShiftId == shift.ShiftId).ToListAsync(cancellationToken))
            .Expected;
        var difference = record.CashMinorUnits - expected;
        var tolerance = MoneyControlPolicy.ResolveDiscrepancyTolerance(
            await db.Branches.Where(branch => branch.BranchId == branchId).Select(branch => branch.ShiftDiscrepancyToleranceMinorUnits).FirstOrDefaultAsync(cancellationToken),
            MoneyControlPolicy.DefaultDiscrepancyToleranceMinorUnits);

        shift.State = ShiftStateNames.Closed;
        shift.ClosedByStaffUserId = staffUserId;
        shift.CountedCashMinorUnits = record.CashMinorUnits;
        shift.ExpectedCashMinorUnits = expected;
        shift.DifferenceMinorUnits = difference;
        shift.ClosingNote = (record.Note ?? string.Empty).Trim();
        shift.ClosedAtUtc = record.OccurredAtUtc;
        if (Math.Abs(difference) > tolerance)
        {
            shift.SignOffReason = UnsignedOfflineCloseReason;
        }

        if (discrepancyNotifier is not null)
        {
            await discrepancyNotifier.NotifyShiftClosedAsync(shift, cancellationToken);
        }

        return null;
    }

    public async Task<string?> OpenShiftAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, IReadOnlySet<string> granted, CancellationToken cancellationToken)
    {
        if (!granted.Contains(OrganizationPermissionNames.OpenShift))
        {
            return OfflineJournalRejectionNames.NotAllowed;
        }

        if (record.ShiftId == Guid.Empty || record.CashMinorUnits < 0 || string.IsNullOrWhiteSpace(record.CurrencyCode))
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        if (await db.Shifts.AnyAsync(shift => shift.ShiftId == record.ShiftId, cancellationToken))
        {
            return OfflineJournalRejectionNames.UnknownTarget;
        }

        if (await db.Shifts.AnyAsync(shift => shift.OrganizationId == organizationId && shift.BranchId == branchId && shift.State == ShiftStateNames.Open, cancellationToken))
        {
            return OfflineJournalRejectionNames.ShiftAlreadyOpen;
        }

        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = record.ShiftId,
            OrganizationId = organizationId,
            BranchId = branchId,
            OpenedByStaffUserId = staffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = record.CurrencyCode.Trim().ToUpperInvariant(),
            StartingCashMinorUnits = record.CashMinorUnits,
            OpeningNote = (record.Note ?? string.Empty).Trim(),
            ClosingNote = string.Empty,
            OpenedAtUtc = record.OccurredAtUtc
        });
        return null;
    }

    private async Task<(ShiftEntity? Entity, string? Refusal)> OpenShiftAsync(
        Guid organizationId, Guid branchId, Guid shiftId, CancellationToken cancellationToken, bool track = false)
    {
        var query = db.Shifts.Where(shift => shift.ShiftId == shiftId && shift.BranchId == branchId && shift.OrganizationId == organizationId);
        var shift = await (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
        return shift switch
        {
            null => (null, OfflineJournalRejectionNames.UnknownTarget),
            { State: ShiftStateNames.Open } => (shift, null),
            _ => (null, OfflineJournalRejectionNames.ShiftClosed)
        };
    }
}
