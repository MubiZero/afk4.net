using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Receipts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Pos;

public sealed class EfShopPosSettlementService(
    PlatformDbContext dbContext,
    IWalletSettlementService walletSettlementService,
    IInventoryCostService inventoryCostService,
    IReceiptNumberGenerator receiptNumberGenerator) : IShopPosSettlementService
{
    private const string SaleReceiptType = "sale";
    private const string RefundReceiptType = "refund";

    public async Task<ShopPosSettlementResult> CreatePaidWalletSaleAsync(
        ShopPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        var aggregatedLines = AggregateLines(request.Lines);
        if (aggregatedLines is null)
        {
            return ShopPosSettlementResult.Reject("invalid_lines");
        }

        var openShifts = await dbContext.Shifts
            .AsNoTracking()
            .Where(candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == request.BranchId &&
                    candidate.State == ShiftStateNames.Open)
            .OrderBy(candidate => candidate.ShiftId)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (openShifts.Count == 0)
        {
            return ShopPosSettlementResult.Reject("open_shift_required");
        }

        if (openShifts.Count > 1)
        {
            return ShopPosSettlementResult.Reject("open_shift_ambiguous");
        }

        var shift = openShifts[0];
        var productIds = aggregatedLines.Keys.ToList();
        var products = await dbContext.PosProducts
            .AsNoTracking()
            .Where(product =>
                product.OrganizationId == request.OrganizationId &&
                product.BranchId == request.BranchId &&
                productIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);
        if (products.Count != productIds.Count ||
            products.Values.Any(product => !product.IsActive || !product.AvailableInShell))
        {
            return ShopPosSettlementResult.Reject("product_unavailable");
        }

        var currencies = products.Values
            .Select(product => product.CurrencyCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (currencies.Count != 1)
        {
            return ShopPosSettlementResult.Reject("mixed_currency");
        }

        var currencyCode = currencies[0];
        if (!string.Equals(shift.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return ShopPosSettlementResult.Reject("mixed_currency");
        }

        Dictionary<Guid, long> stockByProduct;
        try
        {
            stockByProduct = await dbContext.StockMovements
                .AsNoTracking()
                .Where(movement =>
                    movement.OrganizationId == request.OrganizationId &&
                    movement.BranchId == request.BranchId &&
                    productIds.Contains(movement.ProductId))
                .GroupBy(movement => movement.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(movement => (long)movement.QuantityDelta)
                })
                .ToDictionaryAsync(item => item.ProductId, item => item.Quantity, cancellationToken);

            foreach (var stagedMovement in dbContext.ChangeTracker.Entries<StockMovementEntity>()
                         .Where(entry =>
                             entry.State == EntityState.Added &&
                             entry.Entity.OrganizationId == request.OrganizationId &&
                             entry.Entity.BranchId == request.BranchId &&
                             productIds.Contains(entry.Entity.ProductId))
                         .Select(entry => entry.Entity))
            {
                stockByProduct[stagedMovement.ProductId] = checked(
                    stockByProduct.GetValueOrDefault(stagedMovement.ProductId) + stagedMovement.QuantityDelta);
            }
        }
        catch (OverflowException)
        {
            return ShopPosSettlementResult.Reject("stock_quantity_invalid");
        }

        if (stockByProduct.Values.Any(quantity => quantity is > int.MaxValue or < int.MinValue))
        {
            return ShopPosSettlementResult.Reject("stock_quantity_invalid");
        }

        foreach (var (productId, quantity) in aggregatedLines)
        {
            var product = products[productId];
            if (product.TrackStock &&
                !product.AllowNegativeStock &&
                stockByProduct.GetValueOrDefault(productId) < quantity)
            {
                return ShopPosSettlementResult.Reject("out_of_stock");
            }
        }

        List<PosSaleLineEntity> saleLines;
        long totalMinorUnits;
        try
        {
            saleLines = aggregatedLines
                .Select(item => CreateSaleLine(products[item.Key], item.Value))
                .ToList();
            totalMinorUnits = checked(saleLines.Sum(line => line.LineTotalMinorUnits));
        }
        catch (OverflowException)
        {
            return ShopPosSettlementResult.Reject("invalid_amount");
        }

        if (totalMinorUnits <= 0)
        {
            return ShopPosSettlementResult.Reject("invalid_amount");
        }

        var receiptNumber = await receiptNumberGenerator.GenerateAsync(
            request.OrganizationId,
            request.BranchId,
            SaleReceiptType,
            request.Now,
            cancellationToken);
        var branchLocale = await dbContext.Branches
            .AsNoTracking()
            .Where(branch =>
                branch.OrganizationId == request.OrganizationId &&
                branch.BranchId == request.BranchId)
            .Select(branch => branch.PreferredLocale)
            .SingleOrDefaultAsync(cancellationToken) ?? "ru";

        var walletResult = await walletSettlementService.DebitAsync(
            request.OrganizationId,
            request.BranchId,
            request.PlayerAccountId,
            request.SessionId,
            shift.ShiftId,
            totalMinorUnits,
            currencyCode,
            "shop_order",
            request.ReferenceId,
            request.ActorStaffUserId,
            request.Now,
            cancellationToken);
        if (!walletResult.Succeeded || walletResult.Entry is null)
        {
            return ShopPosSettlementResult.Reject(walletResult.ErrorCode ?? "wallet_debit_failed");
        }

        var sale = new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            ShiftId = shift.ShiftId,
            CreatedByStaffUserId = request.ActorStaffUserId,
            PlayerAccountId = request.PlayerAccountId,
            SessionId = request.SessionId,
            State = PosSaleStateNames.Paid,
            CurrencyCode = currencyCode,
            TotalMinorUnits = totalMinorUnits,
            CreatedAtUtc = request.Now,
            PaidAtUtc = request.Now
        };
        foreach (var line in saleLines)
        {
            line.PosSaleId = sale.PosSaleId;
        }

        var payment = new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = sale.OrganizationId,
            BranchId = sale.BranchId,
            PosSaleId = sale.PosSaleId,
            ShiftId = sale.ShiftId,
            CreatedByStaffUserId = request.ActorStaffUserId,
            PaymentKind = "payment",
            Provider = "wallet",
            PaymentMethod = PaymentMethodNames.Wallet,
            CurrencyCode = sale.CurrencyCode,
            AmountMinorUnits = sale.TotalMinorUnits,
            Note = request.ReferenceId,
            CreatedAtUtc = request.Now
        };
        var receipt = new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = sale.OrganizationId,
            BranchId = sale.BranchId,
            PosSaleId = sale.PosSaleId,
            ReceiptNumber = receiptNumber,
            ReceiptType = SaleReceiptType,
            CurrencyCode = sale.CurrencyCode,
            TotalMinorUnits = sale.TotalMinorUnits,
            Locale = branchLocale,
            CreatedAtUtc = request.Now
        };
        var movements = saleLines
            .Where(line => line.TracksStock)
            .Select(line => new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = sale.OrganizationId,
                BranchId = sale.BranchId,
                ProductId = line.ProductId,
                MovementType = StockMovementTypeNames.Sale,
                QuantityDelta = -line.Quantity,
                CurrencyCode = line.CurrencyCode,
                UnitCostMinorUnits = line.UnitCostMinorUnits,
                Reason = request.ReferenceId,
                CreatedByStaffUserId = request.ActorStaffUserId,
                CreatedAtUtc = request.Now
            })
            .ToList();

        dbContext.PosSales.Add(sale);
        dbContext.PosSaleLines.AddRange(saleLines);
        dbContext.Payments.Add(payment);
        dbContext.Receipts.Add(receipt);
        dbContext.StockMovements.AddRange(movements);

        return ShopPosSettlementResult.Ok(sale, saleLines, walletResult.Entry, receipt);
    }

    public async Task<ShopPosSettlementResult> RefundPaidWalletSaleAsync(
        ShopPosRefundRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ShopPosSettlementResult.Reject("refund_reason_required");
        }

        var sale = await dbContext.PosSales
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == request.PosSaleId, cancellationToken);
        if (sale is null)
        {
            return ShopPosSettlementResult.Reject("sale_not_found");
        }

        var lines = await dbContext.PosSaleLines
            .AsNoTracking()
            .Where(line => line.PosSaleId == sale.PosSaleId)
            .OrderBy(line => line.PosSaleLineId)
            .ToListAsync(cancellationToken);

        if (sale.State is not PosSaleStateNames.Paid and not PosSaleStateNames.Refunded)
        {
            return ShopPosSettlementResult.Reject("sale_not_refundable");
        }

        var canonicalDebit = await dbContext.LedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.LedgerEntryId == request.WalletLedgerEntryId,
                cancellationToken);
        if (canonicalDebit is null)
        {
            return ShopPosSettlementResult.Reject("original_debit_not_found");
        }

        if (!IsCanonicalDebitForSale(sale, canonicalDebit))
        {
            return ShopPosSettlementResult.Reject("wallet_debit_mismatch");
        }

        if (sale.State == PosSaleStateNames.Refunded)
        {
            var existingReversal = await LoadSingleReversalAsync(canonicalDebit.LedgerEntryId, cancellationToken);
            var existingPayment = await LoadSingleRefundPaymentAsync(sale.PosSaleId, cancellationToken);
            var existingReceipt = await LoadSingleRefundReceiptAsync(sale.PosSaleId, cancellationToken);

            return existingReversal is null ||
                   existingPayment is null ||
                   existingReceipt is null ||
                   !IsReversalForSale(sale, canonicalDebit, existingReversal) ||
                   !IsRefundPaymentForSale(sale, existingPayment) ||
                   !IsRefundReceiptForSale(sale, existingReceipt)
                ? ShopPosSettlementResult.Reject("refund_incomplete")
                : ShopPosSettlementResult.Ok(sale, lines, existingReversal, existingReceipt);
        }

        if (lines.Count == 0 ||
            lines.Any(line =>
                line.Quantity <= 0 ||
                !string.Equals(line.CurrencyCode, sale.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
        {
            return ShopPosSettlementResult.Reject("sale_snapshot_invalid");
        }

        long lineTotal;
        try
        {
            if (lines.Any(line => line.LineTotalMinorUnits != checked(line.UnitPriceMinorUnits * line.Quantity)))
            {
                return ShopPosSettlementResult.Reject("sale_snapshot_invalid");
            }

            lineTotal = checked(lines.Sum(line => line.LineTotalMinorUnits));
        }
        catch (OverflowException)
        {
            return ShopPosSettlementResult.Reject("sale_snapshot_invalid");
        }

        if (lineTotal != sale.TotalMinorUnits || sale.TotalMinorUnits <= 0)
        {
            return ShopPosSettlementResult.Reject("sale_snapshot_invalid");
        }

        var trackedGroups = lines
            .Where(line => line.TracksStock)
            .GroupBy(line => line.ProductId)
            .ToList();
        if (trackedGroups.Any(group =>
                group.Select(line => line.UnitCostMinorUnits).Distinct().Skip(1).Any()))
        {
            return ShopPosSettlementResult.Reject("cost_snapshot_conflict");
        }

        List<RefundInventoryPlan> inventoryPlans;
        try
        {
            inventoryPlans = trackedGroups
                .Select(group => new RefundInventoryPlan(
                    group.Key,
                    group.Aggregate(0, (quantity, line) => checked(quantity + line.Quantity)),
                    group.First().UnitCostMinorUnits))
                .ToList();
        }
        catch (OverflowException)
        {
            return ShopPosSettlementResult.Reject("sale_snapshot_invalid");
        }

        var trackedProductIds = inventoryPlans.Select(plan => plan.ProductId).ToList();
        var trackedProducts = await dbContext.PosProducts
            .AsNoTracking()
            .Where(product =>
                product.OrganizationId == sale.OrganizationId &&
                product.BranchId == sale.BranchId &&
                trackedProductIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);
        if (trackedProducts.Count != trackedProductIds.Count)
        {
            return ShopPosSettlementResult.Reject("product_unavailable");
        }

        if (inventoryPlans.Any(plan =>
                !string.Equals(
                    trackedProducts[plan.ProductId].CurrencyCode.Trim(),
                    lines.First(line => line.ProductId == plan.ProductId).CurrencyCode.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return ShopPosSettlementResult.Reject("inventory_currency_mismatch");
        }

        var receiptNumber = await receiptNumberGenerator.GenerateAsync(
            sale.OrganizationId,
            sale.BranchId,
            RefundReceiptType,
            request.Now,
            cancellationToken);
        var branchLocale = await dbContext.Branches
            .AsNoTracking()
            .Where(branch =>
                branch.OrganizationId == sale.OrganizationId &&
                branch.BranchId == sale.BranchId)
            .Select(branch => branch.PreferredLocale)
            .SingleOrDefaultAsync(cancellationToken) ?? "ru";

        var walletResult = await walletSettlementService.ReverseAsync(
            canonicalDebit,
            request.ActorStaffUserId,
            "shop_order_refund",
            request.ShopOrderId.ToString("D"),
            request.Now,
            cancellationToken);
        if (!walletResult.Succeeded || walletResult.Entry is null)
        {
            return ShopPosSettlementResult.Reject(walletResult.ErrorCode ?? "wallet_reversal_failed");
        }

        foreach (var plan in inventoryPlans)
        {
            await inventoryCostService.ReconcileInboundAsync(
                sale.OrganizationId,
                sale.BranchId,
                plan.ProductId,
                plan.Quantity,
                lines.First(line => line.ProductId == plan.ProductId).CurrencyCode,
                plan.UnitCostMinorUnits,
                cancellationToken);
        }

        var reason = request.Reason.Trim();
        var refundPayment = new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = sale.OrganizationId,
            BranchId = sale.BranchId,
            PosSaleId = sale.PosSaleId,
            ShiftId = sale.ShiftId,
            CreatedByStaffUserId = request.ActorStaffUserId,
            PaymentKind = "refund",
            Provider = "wallet",
            PaymentMethod = PaymentMethodNames.Wallet,
            CurrencyCode = sale.CurrencyCode,
            AmountMinorUnits = -sale.TotalMinorUnits,
            Note = reason,
            CreatedAtUtc = request.Now
        };
        var refundReceipt = new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = sale.OrganizationId,
            BranchId = sale.BranchId,
            PosSaleId = sale.PosSaleId,
            ReceiptNumber = receiptNumber,
            ReceiptType = RefundReceiptType,
            CurrencyCode = sale.CurrencyCode,
            TotalMinorUnits = sale.TotalMinorUnits,
            Locale = branchLocale,
            CreatedAtUtc = request.Now
        };
        var returnMovements = lines
            .Where(line => line.TracksStock)
            .Select(line => new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = sale.OrganizationId,
                BranchId = sale.BranchId,
                ProductId = line.ProductId,
                MovementType = StockMovementTypeNames.Refund,
                QuantityDelta = line.Quantity,
                CurrencyCode = line.CurrencyCode,
                UnitCostMinorUnits = line.UnitCostMinorUnits,
                Reason = reason,
                CreatedByStaffUserId = request.ActorStaffUserId,
                CreatedAtUtc = request.Now
            })
            .ToList();

        dbContext.Payments.Add(refundPayment);
        dbContext.Receipts.Add(refundReceipt);
        dbContext.StockMovements.AddRange(returnMovements);
        sale.State = PosSaleStateNames.Refunded;
        sale.RefundReason = reason;
        sale.RefundedAtUtc = request.Now;

        return ShopPosSettlementResult.Ok(sale, lines, walletResult.Entry, refundReceipt);
    }

    private static Dictionary<Guid, int>? AggregateLines(IReadOnlyList<ShopOrderLineInput> lines)
    {
        if (lines.Count == 0 || lines.Any(line => line.ProductId == Guid.Empty || line.Quantity <= 0))
        {
            return null;
        }

        try
        {
            return lines
                .GroupBy(line => line.ProductId)
                .ToDictionary(group => group.Key, group => group.Aggregate(0, (quantity, line) => checked(quantity + line.Quantity)));
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static PosSaleLineEntity CreateSaleLine(PosProductEntity product, int quantity)
    {
        var lineTotal = checked(product.PriceMinorUnits * quantity);
        return new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            ProductName = product.Name,
            Quantity = quantity,
            CurrencyCode = product.CurrencyCode.Trim().ToUpperInvariant(),
            UnitPriceMinorUnits = product.PriceMinorUnits,
            UnitCostMinorUnits = product.TrackStock ? product.AvgCostMinorUnits : 0,
            LineTotalMinorUnits = lineTotal,
            TracksStock = product.TrackStock,
            AllowNegativeStock = product.AllowNegativeStock
        };
    }

    private async Task<LedgerEntryEntity?> LoadSingleReversalAsync(
        Guid canonicalDebitId,
        CancellationToken cancellationToken)
    {
        var candidates = dbContext.ChangeTracker.Entries<LedgerEntryEntity>()
            .Where(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.ReversesLedgerEntryId == canonicalDebitId)
            .Select(entry => entry.Entity)
            .ToList();
        var persisted = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.ReversesLedgerEntryId == canonicalDebitId)
            .Take(2)
            .ToListAsync(cancellationToken);
        candidates.AddRange(persisted.Where(entity => candidates.All(candidate => candidate.LedgerEntryId != entity.LedgerEntryId)));
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private async Task<PaymentEntity?> LoadSingleRefundPaymentAsync(
        Guid posSaleId,
        CancellationToken cancellationToken)
    {
        var candidates = dbContext.ChangeTracker.Entries<PaymentEntity>()
            .Where(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.PosSaleId == posSaleId &&
                entry.Entity.PaymentKind == "refund")
            .Select(entry => entry.Entity)
            .ToList();
        var persisted = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.PosSaleId == posSaleId && payment.PaymentKind == "refund")
            .Take(2)
            .ToListAsync(cancellationToken);
        candidates.AddRange(persisted.Where(entity => candidates.All(candidate => candidate.PaymentId != entity.PaymentId)));
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private async Task<ReceiptEntity?> LoadSingleRefundReceiptAsync(
        Guid posSaleId,
        CancellationToken cancellationToken)
    {
        var candidates = dbContext.ChangeTracker.Entries<ReceiptEntity>()
            .Where(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.PosSaleId == posSaleId &&
                entry.Entity.ReceiptType == RefundReceiptType)
            .Select(entry => entry.Entity)
            .ToList();
        var persisted = await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.PosSaleId == posSaleId && receipt.ReceiptType == RefundReceiptType)
            .Take(2)
            .ToListAsync(cancellationToken);
        candidates.AddRange(persisted.Where(entity => candidates.All(candidate => candidate.ReceiptId != entity.ReceiptId)));
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool IsCanonicalDebitForSale(PosSaleEntity sale, LedgerEntryEntity debit) =>
        sale.PlayerAccountId is Guid playerAccountId &&
        debit.OrganizationId == sale.OrganizationId &&
        debit.BranchId == sale.BranchId &&
        debit.PlayerAccountId == playerAccountId &&
        debit.SessionId == sale.SessionId &&
        debit.PlayerPackageId is null &&
        debit.ShiftId == sale.ShiftId &&
        debit.EntryType == LedgerEntryTypeNames.WalletPayment &&
        debit.AccountType == LedgerAccountTypeNames.Wallet &&
        debit.AmountMinorUnits == -sale.TotalMinorUnits &&
        debit.QuantitySeconds == 0 &&
        string.Equals(debit.CurrencyCode, sale.CurrencyCode, StringComparison.OrdinalIgnoreCase);

    private static bool IsReversalForSale(
        PosSaleEntity sale,
        LedgerEntryEntity canonicalDebit,
        LedgerEntryEntity reversal) =>
        reversal.ReversesLedgerEntryId == canonicalDebit.LedgerEntryId &&
        reversal.OrganizationId == sale.OrganizationId &&
        reversal.BranchId == sale.BranchId &&
        reversal.PlayerAccountId == canonicalDebit.PlayerAccountId &&
        reversal.SessionId == sale.SessionId &&
        reversal.PlayerPackageId == canonicalDebit.PlayerPackageId &&
        reversal.ShiftId == sale.ShiftId &&
        reversal.EntryType == LedgerEntryTypeNames.Reversal &&
        reversal.AccountType == LedgerAccountTypeNames.Wallet &&
        reversal.AmountMinorUnits == -canonicalDebit.AmountMinorUnits &&
        reversal.QuantitySeconds == 0 &&
        string.Equals(reversal.CurrencyCode, sale.CurrencyCode, StringComparison.OrdinalIgnoreCase);

    private static bool IsRefundPaymentForSale(PosSaleEntity sale, PaymentEntity payment) =>
        payment.OrganizationId == sale.OrganizationId &&
        payment.BranchId == sale.BranchId &&
        payment.PosSaleId == sale.PosSaleId &&
        payment.SessionId is null &&
        payment.ShiftId == sale.ShiftId &&
        payment.PaymentKind == "refund" &&
        payment.Provider == "wallet" &&
        payment.PaymentMethod == PaymentMethodNames.Wallet &&
        payment.AmountMinorUnits == -sale.TotalMinorUnits &&
        string.Equals(payment.CurrencyCode, sale.CurrencyCode, StringComparison.OrdinalIgnoreCase);

    private static bool IsRefundReceiptForSale(PosSaleEntity sale, ReceiptEntity receipt) =>
        receipt.OrganizationId == sale.OrganizationId &&
        receipt.BranchId == sale.BranchId &&
        receipt.PosSaleId == sale.PosSaleId &&
        receipt.SessionId is null &&
        receipt.ReceiptType == RefundReceiptType &&
        receipt.TotalMinorUnits == sale.TotalMinorUnits &&
        !string.IsNullOrWhiteSpace(receipt.ReceiptNumber) &&
        string.Equals(receipt.CurrencyCode, sale.CurrencyCode, StringComparison.OrdinalIgnoreCase);

    private sealed record RefundInventoryPlan(Guid ProductId, int Quantity, long UnitCostMinorUnits);
}
