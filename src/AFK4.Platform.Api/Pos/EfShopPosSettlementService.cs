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

        var shift = await dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == request.BranchId &&
                    candidate.State == ShiftStateNames.Open,
                cancellationToken);
        if (shift is null)
        {
            return ShopPosSettlementResult.Reject("open_shift_required");
        }

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
        var stockByProduct = await dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.OrganizationId == request.OrganizationId &&
                movement.BranchId == request.BranchId &&
                productIds.Contains(movement.ProductId))
            .GroupBy(movement => movement.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(movement => movement.QuantityDelta) })
            .ToDictionaryAsync(item => item.ProductId, item => item.Quantity, cancellationToken);

        foreach (var stagedMovement in dbContext.ChangeTracker.Entries<StockMovementEntity>()
                     .Where(entry =>
                         entry.State == EntityState.Added &&
                         entry.Entity.OrganizationId == request.OrganizationId &&
                         entry.Entity.BranchId == request.BranchId &&
                         productIds.Contains(entry.Entity.ProductId))
                     .Select(entry => entry.Entity))
        {
            stockByProduct[stagedMovement.ProductId] =
                stockByProduct.GetValueOrDefault(stagedMovement.ProductId) + stagedMovement.QuantityDelta;
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
            .Where(line => line.TrackStock)
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

        if (sale.State == PosSaleStateNames.Refunded)
        {
            var existingReversal = dbContext.ChangeTracker.Entries<LedgerEntryEntity>()
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .SingleOrDefault(entry => entry.ReversesLedgerEntryId == request.WalletLedgerEntryId)
                ?? await dbContext.LedgerEntries
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        entry => entry.ReversesLedgerEntryId == request.WalletLedgerEntryId,
                        cancellationToken);
            var existingReceipt = dbContext.ChangeTracker.Entries<ReceiptEntity>()
                .Where(entry => entry.State == EntityState.Added)
                .Select(entry => entry.Entity)
                .SingleOrDefault(receipt =>
                    receipt.PosSaleId == sale.PosSaleId &&
                    receipt.ReceiptType == RefundReceiptType)
                ?? await dbContext.Receipts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        receipt =>
                            receipt.PosSaleId == sale.PosSaleId &&
                            receipt.ReceiptType == RefundReceiptType,
                        cancellationToken);

            return existingReversal is null || existingReceipt is null
                ? ShopPosSettlementResult.Reject("refund_incomplete")
                : ShopPosSettlementResult.Ok(sale, lines, existingReversal, existingReceipt);
        }

        if (sale.State != PosSaleStateNames.Paid)
        {
            return ShopPosSettlementResult.Reject("sale_not_refundable");
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

        var canonicalDebit = await dbContext.LedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.LedgerEntryId == request.WalletLedgerEntryId,
                cancellationToken);
        if (canonicalDebit is null)
        {
            return ShopPosSettlementResult.Reject("original_debit_not_found");
        }

        if (sale.PlayerAccountId is not Guid playerAccountId ||
            canonicalDebit.OrganizationId != sale.OrganizationId ||
            canonicalDebit.BranchId != sale.BranchId ||
            canonicalDebit.PlayerAccountId != playerAccountId ||
            canonicalDebit.SessionId != sale.SessionId ||
            canonicalDebit.ShiftId != sale.ShiftId ||
            canonicalDebit.EntryType != LedgerEntryTypeNames.WalletPayment ||
            canonicalDebit.AccountType != LedgerAccountTypeNames.Wallet ||
            canonicalDebit.AmountMinorUnits != -sale.TotalMinorUnits ||
            !string.Equals(canonicalDebit.CurrencyCode, sale.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return ShopPosSettlementResult.Reject("wallet_debit_mismatch");
        }

        var trackedGroups = lines
            .Where(line => line.TrackStock)
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
                trackedProductIds.Contains(product.ProductId) &&
                product.TrackStock)
            .Select(product => product.ProductId)
            .ToListAsync(cancellationToken);
        if (trackedProducts.Count != trackedProductIds.Count)
        {
            return ShopPosSettlementResult.Reject("product_unavailable");
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
            .Where(line => line.TrackStock)
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
            TrackStock = product.TrackStock,
            AllowNegativeStock = product.AllowNegativeStock
        };
    }

    private sealed record RefundInventoryPlan(Guid ProductId, int Quantity, long UnitCostMinorUnits);
}
