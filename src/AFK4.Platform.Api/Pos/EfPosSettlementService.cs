using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Receipts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Pos;

public sealed class EfPosSettlementService(
    PlatformDbContext dbContext,
    IWalletSettlementService walletSettlementService,
    IInventoryCostService inventoryCostService,
    IReceiptNumberGenerator receiptNumberGenerator,
    TimeProvider timeProvider,
    ILowStockNotifier? lowStockNotifier = null,
    ILogger<EfPosSettlementService>? logger = null) : IPosSettlementService
{
    private const string SettlementOperation = "pos-sale-settle";
    private const string RefundOperation = "pos-sale-refund";
    private const string SaleReceiptType = "sale";
    private const string RefundReceiptType = "refund";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<PosSaleDto>> SettleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        SettlePosSaleRequest request,
        CancellationToken cancellationToken)
    {
        // The dependency marks Inventory as the owner of cost policy. Outbound settlement deliberately
        // consumes only the immutable cost/tracking snapshots captured on each POS sale line.
        _ = inventoryCostService;

        var saleScope = await dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
        if (saleScope is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("POS sale was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("invalid_payment_split");
        }

        var normalizedRequest = NormalizeRequest(request);
        if (normalizedRequest is null || normalizedRequest.OrganizationId != saleScope.OrganizationId)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("invalid_payment_split");
        }

        if (normalizedRequest.Payments.Any(part =>
                !CurrencyEquals(part.Amount.CurrencyCode, saleScope.CurrencyCode)))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("mixed_currency");
        }

        if (!TryValidateSplit(normalizedRequest.Payments, saleScope.CurrencyCode, saleScope.TotalMinorUnits))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("invalid_payment_split");
        }

        var requestHashInput = new
        {
            PosSaleId = posSaleId,
            Request = normalizedRequest
        };
        var replay = await GetExistingIdempotencyAsync(
            saleScope.OrganizationId,
            saleScope.BranchId,
            normalizedRequest.IdempotencyKey,
            requestHashInput,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var walletPart = normalizedRequest.Payments
            .SingleOrDefault(part => part.PaymentMethod == PaymentMethodNames.Wallet);
        if (walletPart is not null && saleScope.PlayerAccountId is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("wallet_player_required");
        }

        IReadOnlyList<Guid> productIdsToNotify = [];
        var result = await ExecuteInTransactionAsync(async () =>
        {
            var sale = await dbContext.PosSales
                .SingleAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
            if (sale.State is not PosSaleStateNames.Draft and not PosSaleStateNames.PendingPayment)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("sale_not_payable");
            }

            var shift = await dbContext.Shifts
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate =>
                    candidate.ShiftId == sale.ShiftId &&
                    candidate.OrganizationId == sale.OrganizationId &&
                    candidate.BranchId == sale.BranchId &&
                    candidate.State == ShiftStateNames.Open,
                    cancellationToken);
            if (shift is null || !CurrencyEquals(shift.CurrencyCode, sale.CurrencyCode))
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("open_shift_required");
            }

            var lines = await dbContext.PosSaleLines
                .Where(line => line.PosSaleId == sale.PosSaleId)
                .OrderBy(line => line.ProductName)
                .ThenBy(line => line.ProductId)
                .ToListAsync(cancellationToken);
            var stockError = await ValidateStockAsync(sale, lines, cancellationToken);
            if (stockError is not null)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid(stockError);
            }

            var now = timeProvider.GetUtcNow();
            LedgerEntryEntity? walletDebit = null;
            if (walletPart is not null && sale.PlayerAccountId is Guid playerAccountId)
            {
                var playerOwnedBySaleScope = await dbContext.PlayerAccounts
                    .AsNoTracking()
                    .AnyAsync(player =>
                        player.PlayerAccountId == playerAccountId &&
                        player.OrganizationId == sale.OrganizationId &&
                        player.HomeBranchId == sale.BranchId &&
                        player.IsActive,
                        cancellationToken);
                if (!playerOwnedBySaleScope)
                {
                    return BillingCommandServiceResult<PosSaleDto>.Invalid("wallet_player_invalid");
                }

                var debit = await walletSettlementService.DebitAsync(
                    sale.OrganizationId,
                    sale.BranchId,
                    playerAccountId,
                    sessionId: null,
                    sale.ShiftId,
                    walletPart.Amount.MinorUnits,
                    sale.CurrencyCode,
                    $"POS sale {sale.PosSaleId:D}",
                    normalizedRequest.Note,
                    actorStaffUserId,
                    now,
                    cancellationToken);
                if (!debit.Succeeded || debit.Entry is null)
                {
                    return BillingCommandServiceResult<PosSaleDto>.Invalid(debit.ErrorCode ?? "wallet_payment_failed");
                }

                walletDebit = debit.Entry;
            }

            foreach (var line in lines.Where(line => line.TracksStock))
            {
                dbContext.StockMovements.Add(new StockMovementEntity
                {
                    StockMovementId = Guid.NewGuid(),
                    OrganizationId = sale.OrganizationId,
                    BranchId = sale.BranchId,
                    ProductId = line.ProductId,
                    MovementType = StockMovementTypeNames.Sale,
                    QuantityDelta = -line.Quantity,
                    CurrencyCode = line.CurrencyCode,
                    UnitCostMinorUnits = line.UnitCostMinorUnits,
                    Reason = $"POS sale {sale.PosSaleId:D}",
                    CreatedByStaffUserId = actorStaffUserId,
                    CreatedAtUtc = now
                });
            }

            foreach (var part in normalizedRequest.Payments)
            {
                var isWallet = part.PaymentMethod == PaymentMethodNames.Wallet;
                dbContext.Payments.Add(new PaymentEntity
                {
                    PaymentId = Guid.NewGuid(),
                    OrganizationId = sale.OrganizationId,
                    BranchId = sale.BranchId,
                    PosSaleId = sale.PosSaleId,
                    ShiftId = sale.ShiftId,
                    CreatedByStaffUserId = actorStaffUserId,
                    PaymentKind = "payment",
                    Provider = isWallet ? "wallet" : "manual",
                    PaymentMethod = part.PaymentMethod,
                    LedgerEntryId = isWallet ? walletDebit!.LedgerEntryId : null,
                    CurrencyCode = sale.CurrencyCode,
                    AmountMinorUnits = part.Amount.MinorUnits,
                    Note = normalizedRequest.Note,
                    CreatedAtUtc = now
                });
            }

            var receiptNumber = await receiptNumberGenerator.GenerateAsync(
                sale.OrganizationId,
                sale.BranchId,
                SaleReceiptType,
                now,
                cancellationToken);
            var branchLocale = await dbContext.Branches
                .Where(branch => branch.BranchId == sale.BranchId)
                .Select(branch => branch.PreferredLocale)
                .FirstOrDefaultAsync(cancellationToken) ?? "ru";
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
                CreatedAtUtc = now
            };
            dbContext.Receipts.Add(receipt);

            sale.State = PosSaleStateNames.Paid;
            sale.PaidAtUtc = now;
            var response = ToDto(sale, lines, receipt);
            AddIdempotencyRecord(
                sale.OrganizationId,
                sale.BranchId,
                normalizedRequest.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            productIdsToNotify = lines
                .Where(line => line.TracksStock)
                .Select(line => line.ProductId)
                .Distinct()
                .ToList();
            return BillingCommandServiceResult<PosSaleDto>.Ok(response);
        }, () => GetExistingIdempotencyAsync(
            saleScope.OrganizationId,
            saleScope.BranchId,
            normalizedRequest.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);

        if (result.Succeeded && lowStockNotifier is not null && productIdsToNotify.Count > 0)
        {
            try
            {
                // Settlement is already durable. Notification latency must not turn a committed
                // financial command into an ambiguous client failure, and request cancellation is
                // no longer authoritative after commit.
                await lowStockNotifier.EvaluateProductsAsync(
                    saleScope.OrganizationId,
                    saleScope.BranchId,
                    productIdsToNotify,
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger?.LogWarning(
                    exception,
                    "Low-stock evaluation failed after POS sale {PosSaleId} committed.",
                    posSaleId);
            }
        }

        return result;
    }

    public async Task<BillingCommandServiceResult<PosSaleDto>> RefundAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        var saleScope = await dbContext.PosSales
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
        if (saleScope is null)
        {
            return BillingCommandServiceResult<PosSaleDto>.Missing("POS sale was not found.");
        }

        if (request.OrganizationId != saleScope.OrganizationId)
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Organization id does not match the POS sale.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Refund reason is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("Idempotency key is required.");
        }

        var normalizedRequest = request with
        {
            Reason = request.Reason.Trim(),
            IdempotencyKey = request.IdempotencyKey.Trim()
        };
        var requestHashInput = new { PosSaleId = posSaleId, Request = normalizedRequest };
        var replay = await GetExistingRefundIdempotencyAsync(
            saleScope.OrganizationId,
            saleScope.BranchId,
            normalizedRequest.IdempotencyKey,
            requestHashInput,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var sale = await dbContext.PosSales
                .SingleAsync(candidate => candidate.PosSaleId == posSaleId, cancellationToken);
            if (sale.State != PosSaleStateNames.Paid)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("Only paid POS sales can be refunded.");
            }

            var priorRefundEffectExists = await dbContext.Payments
                    .AsNoTracking()
                    .AnyAsync(payment =>
                        payment.PosSaleId == sale.PosSaleId &&
                        payment.PaymentKind == "refund",
                        cancellationToken) ||
                await dbContext.Receipts
                    .AsNoTracking()
                    .AnyAsync(receipt =>
                        receipt.PosSaleId == sale.PosSaleId &&
                        receipt.ReceiptType == RefundReceiptType,
                        cancellationToken);
            if (priorRefundEffectExists)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("refund_incomplete");
            }

            var lines = await dbContext.PosSaleLines
                .Where(line => line.PosSaleId == sale.PosSaleId)
                .OrderBy(line => line.ProductName)
                .ThenBy(line => line.ProductId)
                .ToListAsync(cancellationToken);
            var snapshotError = ValidateSaleSnapshot(sale, lines);
            if (snapshotError is not null)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid(snapshotError);
            }

            var originalPayments = await LoadOriginalPaymentMixAsync(sale, cancellationToken);
            var paymentError = ValidateOriginalPayments(sale, originalPayments);
            if (paymentError is not null)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid(paymentError);
            }

            var walletDebits = new Dictionary<Guid, LedgerEntryEntity>();
            foreach (var payment in originalPayments.Where(payment => payment.PaymentMethod == PaymentMethodNames.Wallet))
            {
                var debit = await dbContext.LedgerEntries
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        entry => entry.LedgerEntryId == payment.LedgerEntryId!.Value,
                        cancellationToken);
                if (debit is null || !IsCanonicalWalletDebit(sale, payment, debit))
                {
                    return BillingCommandServiceResult<PosSaleDto>.Invalid("wallet_debit_mismatch");
                }

                walletDebits.Add(payment.PaymentId, debit);
            }

            var trackedProductLines = lines
                .Where(line => line.TracksStock)
                .GroupBy(line => line.ProductId)
                .ToList();
            if (trackedProductLines.Any(group =>
                    group.Select(line => line.UnitCostMinorUnits).Distinct().Skip(1).Any()))
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid(
                    "Refunded lines for the same product must have the same cost snapshot.");
            }

            var trackedProductIds = trackedProductLines.Select(group => group.Key).ToList();
            var trackedProducts = await dbContext.PosProducts
                .AsNoTracking()
                .Where(product =>
                    product.OrganizationId == sale.OrganizationId &&
                    product.BranchId == sale.BranchId &&
                    trackedProductIds.Contains(product.ProductId))
                .ToDictionaryAsync(product => product.ProductId, cancellationToken);
            if (trackedProducts.Count != trackedProductIds.Count)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("product_unavailable");
            }

            if (trackedProductLines.Any(group =>
                    !CurrencyEquals(trackedProducts[group.Key].CurrencyCode, group.First().CurrencyCode)))
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("inventory_currency_mismatch");
            }

            var inventoryQuantities = new Dictionary<Guid, int>();
            try
            {
                foreach (var productLines in trackedProductLines)
                {
                    inventoryQuantities.Add(
                        productLines.Key,
                        productLines.Aggregate(0, (total, line) => checked(total + line.Quantity)));
                }
            }
            catch (OverflowException)
            {
                return BillingCommandServiceResult<PosSaleDto>.Invalid("sale_snapshot_invalid");
            }

            var now = timeProvider.GetUtcNow();
            var refundPayments = new List<PaymentEntity>(originalPayments.Count);
            foreach (var payment in originalPayments)
            {
                LedgerEntryEntity? reversal = null;
                if (payment.PaymentMethod == PaymentMethodNames.Wallet)
                {
                    var reversed = await walletSettlementService.ReverseAsync(
                        walletDebits[payment.PaymentId],
                        actorStaffUserId,
                        $"POS sale refund {sale.PosSaleId:D}",
                        normalizedRequest.Reason,
                        now,
                        cancellationToken);
                    if (!reversed.Succeeded || reversed.Entry is null)
                    {
                        return BillingCommandServiceResult<PosSaleDto>.Invalid(
                            reversed.ErrorCode ?? "wallet_reversal_failed");
                    }

                    reversal = reversed.Entry;
                }

                refundPayments.Add(new PaymentEntity
                {
                    PaymentId = Guid.NewGuid(),
                    OrganizationId = sale.OrganizationId,
                    BranchId = sale.BranchId,
                    PosSaleId = sale.PosSaleId,
                    ShiftId = sale.ShiftId,
                    CreatedByStaffUserId = actorStaffUserId,
                    PaymentKind = "refund",
                    Provider = payment.Provider,
                    PaymentMethod = payment.PaymentMethod,
                    LedgerEntryId = reversal?.LedgerEntryId,
                    CurrencyCode = sale.CurrencyCode,
                    AmountMinorUnits = -payment.AmountMinorUnits,
                    Note = normalizedRequest.Reason,
                    CreatedAtUtc = now
                });
            }

            foreach (var productLines in trackedProductLines)
            {
                await inventoryCostService.ReconcileInboundAsync(
                    sale.OrganizationId,
                    sale.BranchId,
                    productLines.Key,
                    inventoryQuantities[productLines.Key],
                    productLines.First().CurrencyCode,
                    productLines.First().UnitCostMinorUnits,
                    cancellationToken);

                foreach (var line in productLines)
                {
                    dbContext.StockMovements.Add(new StockMovementEntity
                    {
                        StockMovementId = Guid.NewGuid(),
                        OrganizationId = sale.OrganizationId,
                        BranchId = sale.BranchId,
                        ProductId = line.ProductId,
                        MovementType = StockMovementTypeNames.Refund,
                        QuantityDelta = line.Quantity,
                        CurrencyCode = line.CurrencyCode,
                        UnitCostMinorUnits = line.UnitCostMinorUnits,
                        Reason = normalizedRequest.Reason,
                        CreatedByStaffUserId = actorStaffUserId,
                        CreatedAtUtc = now
                    });
                }
            }

            var receiptNumber = await receiptNumberGenerator.GenerateAsync(
                sale.OrganizationId,
                sale.BranchId,
                RefundReceiptType,
                now,
                cancellationToken);
            var branchLocale = await dbContext.Branches
                .Where(branch => branch.BranchId == sale.BranchId)
                .Select(branch => branch.PreferredLocale)
                .FirstOrDefaultAsync(cancellationToken) ?? "ru";
            var receipt = new ReceiptEntity
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
                CreatedAtUtc = now
            };

            dbContext.Payments.AddRange(refundPayments);
            dbContext.Receipts.Add(receipt);
            sale.State = PosSaleStateNames.Refunded;
            sale.RefundReason = normalizedRequest.Reason;
            sale.RefundedAtUtc = now;
            var response = ToDto(sale, lines, receipt);
            AddRefundIdempotencyRecord(
                sale.OrganizationId,
                sale.BranchId,
                normalizedRequest.IdempotencyKey,
                requestHashInput,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return BillingCommandServiceResult<PosSaleDto>.Ok(response);
        }, () => GetExistingRefundIdempotencyAsync(
            saleScope.OrganizationId,
            saleScope.BranchId,
            normalizedRequest.IdempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    private static SettlePosSaleRequest? NormalizeRequest(SettlePosSaleRequest request)
    {
        if (request.Payments is null ||
            request.Payments.Any(part => part is null || part.Amount is null))
        {
            return null;
        }

        var normalizedParts = request.Payments
            .Select(part => new PaymentPartDto(
                part.PaymentMethod?.Trim().ToLowerInvariant() ?? string.Empty,
                new MoneyDto(
                    part.Amount.CurrencyCode?.Trim().ToUpperInvariant() ?? string.Empty,
                    part.Amount.MinorUnits)))
            .OrderBy(part => part.PaymentMethod, StringComparer.Ordinal)
            .ToList();
        return new SettlePosSaleRequest(
            request.OrganizationId,
            normalizedParts,
            request.Note?.Trim() ?? string.Empty,
            request.IdempotencyKey.Trim());
    }

    private static bool TryValidateSplit(
        IReadOnlyList<PaymentPartDto> payments,
        string saleCurrency,
        long saleTotal)
    {
        if (payments.Count == 0 ||
            payments.Any(part => !PaymentMethodNames.IsValid(part.PaymentMethod)) ||
            payments.Select(part => part.PaymentMethod).Distinct(StringComparer.Ordinal).Count() != payments.Count ||
            payments.Any(part => part.Amount.MinorUnits <= 0 || !CurrencyEquals(part.Amount.CurrencyCode, saleCurrency)))
        {
            return false;
        }

        try
        {
            return payments.Aggregate(0L, (total, part) => checked(total + part.Amount.MinorUnits)) == saleTotal;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private async Task<string?> ValidateStockAsync(
        PosSaleEntity sale,
        IReadOnlyList<PosSaleLineEntity> lines,
        CancellationToken cancellationToken)
    {
        foreach (var group in lines.Where(line => line.TracksStock).GroupBy(line => line.ProductId))
        {
            if (group.All(line => line.AllowNegativeStock))
            {
                continue;
            }

            int quantity;
            try
            {
                quantity = group.Aggregate(0, (total, line) => checked(total + line.Quantity));
            }
            catch (OverflowException)
            {
                return "insufficient_stock";
            }

            var stockOnHand = await dbContext.StockMovements
                .Where(movement =>
                    movement.OrganizationId == sale.OrganizationId &&
                    movement.BranchId == sale.BranchId &&
                    movement.ProductId == group.Key)
                .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;
            if (stockOnHand - quantity < 0)
            {
                return "insufficient_stock";
            }
        }

        return null;
    }

    private async Task<BillingCommandServiceResult<PosSaleDto>?> GetExistingIdempotencyAsync<TRequest>(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.Operation == SettlementOperation &&
                candidate.IdempotencyKeyHash == BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
                cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, HashRequest(request), StringComparison.Ordinal))
        {
            return BillingCommandServiceResult<PosSaleDto>.RequestConflict("idempotency_conflict");
        }

        var response = JsonSerializer.Deserialize<PosSaleDto>(existing.ResponseJson, JsonOptions);
        return response is null
            ? BillingCommandServiceResult<PosSaleDto>.Invalid("Stored idempotent response could not be read.")
            : BillingCommandServiceResult<PosSaleDto>.Ok(response);
    }

    private static string? ValidateSaleSnapshot(PosSaleEntity sale, IReadOnlyList<PosSaleLineEntity> lines)
    {
        if (lines.Count == 0 || sale.TotalMinorUnits <= 0 || lines.Any(line =>
                line.Quantity <= 0 ||
                !CurrencyEquals(line.CurrencyCode, sale.CurrencyCode)))
        {
            return "sale_snapshot_invalid";
        }

        try
        {
            if (lines.Any(line => line.LineTotalMinorUnits != checked(line.UnitPriceMinorUnits * line.Quantity)) ||
                checked(lines.Sum(line => line.LineTotalMinorUnits)) != sale.TotalMinorUnits)
            {
                return "sale_snapshot_invalid";
            }
        }
        catch (OverflowException)
        {
            return "sale_snapshot_invalid";
        }

        return null;
    }

    private static string? ValidateOriginalPayments(
        PosSaleEntity sale,
        IReadOnlyList<PaymentEntity> payments)
    {
        if (payments.Count == 0 ||
            payments.Select(payment => payment.PaymentMethod).Distinct(StringComparer.Ordinal).Count() != payments.Count ||
            payments.Any(payment =>
                payment.OrganizationId != sale.OrganizationId ||
                payment.BranchId != sale.BranchId ||
                payment.PosSaleId != sale.PosSaleId ||
                payment.SessionId is not null ||
                payment.ShiftId != sale.ShiftId ||
                !PaymentMethodNames.IsValid(payment.PaymentMethod) ||
                payment.AmountMinorUnits <= 0 ||
                !CurrencyEquals(payment.CurrencyCode, sale.CurrencyCode) ||
                (payment.PaymentMethod == PaymentMethodNames.Wallet
                    ? payment.LedgerEntryId is null || payment.Provider != "wallet"
                    : payment.LedgerEntryId is not null || payment.Provider != "manual")))
        {
            return "payment_snapshot_invalid";
        }

        try
        {
            return checked(payments.Sum(payment => payment.AmountMinorUnits)) == sale.TotalMinorUnits
                ? null
                : "payment_snapshot_invalid";
        }
        catch (OverflowException)
        {
            return "payment_snapshot_invalid";
        }
    }

    private async Task<IReadOnlyList<PaymentEntity>> LoadOriginalPaymentMixAsync(
        PosSaleEntity sale,
        CancellationToken cancellationToken)
    {
        var directPayments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.PosSaleId == sale.PosSaleId && payment.PaymentKind == "payment")
            .OrderBy(payment => payment.CreatedAtUtc)
            .ThenBy(payment => payment.PaymentId)
            .ToListAsync(cancellationToken);
        if (directPayments.Count > 0 || sale.SessionId is not Guid sessionId)
        {
            return directPayments;
        }

        // Compatibility for historical attached sales settled by the old unified session-checkout
        // path. Only a single non-wallet method is unambiguous: mixed parts cannot be allocated to
        // the sale, and historical wallet rows have no ledger link that could be reversed safely.
        var sessionPayments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.SessionId == sessionId && payment.PaymentKind == "payment")
            .OrderBy(payment => payment.CreatedAtUtc)
            .ThenBy(payment => payment.PaymentId)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (sessionPayments.Count != 1 ||
            sessionPayments[0].PaymentMethod == PaymentMethodNames.Wallet ||
            sessionPayments[0].AmountMinorUnits < sale.TotalMinorUnits)
        {
            return directPayments;
        }

        var source = sessionPayments[0];
        return
        [
            new PaymentEntity
            {
                PaymentId = source.PaymentId,
                OrganizationId = source.OrganizationId,
                BranchId = source.BranchId,
                PosSaleId = sale.PosSaleId,
                SessionId = null,
                ShiftId = source.ShiftId,
                CreatedByStaffUserId = source.CreatedByStaffUserId,
                PaymentKind = source.PaymentKind,
                Provider = source.Provider,
                PaymentMethod = source.PaymentMethod,
                CurrencyCode = source.CurrencyCode,
                AmountMinorUnits = sale.TotalMinorUnits,
                Note = source.Note,
                CreatedAtUtc = source.CreatedAtUtc
            }
        ];
    }

    private static bool IsCanonicalWalletDebit(
        PosSaleEntity sale,
        PaymentEntity payment,
        LedgerEntryEntity debit) =>
        sale.PlayerAccountId is Guid playerAccountId &&
        debit.OrganizationId == sale.OrganizationId &&
        debit.BranchId == sale.BranchId &&
        debit.PlayerAccountId == playerAccountId &&
        (debit.SessionId is null || debit.SessionId == sale.SessionId) &&
        debit.PlayerPackageId is null &&
        debit.ShiftId == sale.ShiftId &&
        debit.EntryType == LedgerEntryTypeNames.WalletPayment &&
        debit.AccountType == LedgerAccountTypeNames.Wallet &&
        debit.AmountMinorUnits == -payment.AmountMinorUnits &&
        debit.QuantitySeconds == 0 &&
        CurrencyEquals(debit.CurrencyCode, sale.CurrencyCode);

    private async Task<BillingCommandServiceResult<PosSaleDto>?> GetExistingRefundIdempotencyAsync<TRequest>(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.Operation == RefundOperation &&
                candidate.IdempotencyKeyHash == BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
                cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, HashRequest(request), StringComparison.Ordinal))
        {
            return BillingCommandServiceResult<PosSaleDto>.RequestConflict("version_conflict");
        }

        var response = JsonSerializer.Deserialize<PosSaleDto>(existing.ResponseJson, JsonOptions);
        return response is null
            ? BillingCommandServiceResult<PosSaleDto>.Invalid("Stored idempotent response could not be read.")
            : BillingCommandServiceResult<PosSaleDto>.Ok(response);
    }

    private void AddRefundIdempotencyRecord<TRequest>(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        TRequest request,
        PosSaleDto response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = RefundOperation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private void AddIdempotencyRecord<TRequest>(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        TRequest request,
        PosSaleDto response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = SettlementOperation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private async Task<BillingCommandServiceResult<PosSaleDto>> ExecuteInTransactionAsync(
        Func<Task<BillingCommandServiceResult<PosSaleDto>>> action,
        Func<Task<BillingCommandServiceResult<PosSaleDto>?>> recoverIdempotencyRaceAsync,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            try
            {
                var result = await action();
                if (!result.Succeeded)
                {
                    dbContext.ChangeTracker.Clear();
                }

                return result;
            }
            catch (DbUpdateException)
            {
                dbContext.ChangeTracker.Clear();
                var recovered = await recoverIdempotencyRaceAsync();
                if (recovered is not null)
                {
                    return recovered;
                }

                throw;
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            var result = await action();
            if (!result.Succeeded)
            {
                await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
                dbContext.ChangeTracker.Clear();
                return result;
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (RelationalFailureClassifier.IsSerializationFailure(exception))
        {
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            return BillingCommandServiceResult<PosSaleDto>.RequestConflict("version_conflict");
        }
        catch (DbUpdateException)
        {
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            var recovered = await recoverIdempotencyRaceAsync();
            if (recovered is not null)
            {
                return recovered;
            }

            throw;
        }
        catch
        {
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private static string HashRequest<TRequest>(TRequest request) =>
        BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));

    private static bool CurrencyEquals(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static PosSaleDto ToDto(
        PosSaleEntity sale,
        IReadOnlyList<PosSaleLineEntity> lines,
        ReceiptEntity receipt) =>
        new(
            sale.PosSaleId,
            sale.OrganizationId,
            sale.BranchId,
            sale.ShiftId,
            sale.State,
            lines.Select(line => new PosSaleLineDto(
                line.ProductId,
                line.ProductName,
                line.Quantity,
                new MoneyDto(line.CurrencyCode, line.UnitPriceMinorUnits),
                new MoneyDto(line.CurrencyCode, line.LineTotalMinorUnits))).ToList(),
            new MoneyDto(sale.CurrencyCode, sale.TotalMinorUnits),
            sale.CreatedByStaffUserId,
            sale.CreatedAtUtc,
            sale.PaidAtUtc,
            sale.RefundedAtUtc,
            sale.VoidedAtUtc,
            new ReceiptDto(
                receipt.ReceiptId,
                receipt.OrganizationId,
                receipt.BranchId,
                receipt.PosSaleId,
                receipt.ReceiptNumber,
                receipt.ReceiptType,
                new MoneyDto(receipt.CurrencyCode, receipt.TotalMinorUnits),
                receipt.CreatedAtUtc,
                receipt.SessionId),
            sale.PlayerAccountId);
}
