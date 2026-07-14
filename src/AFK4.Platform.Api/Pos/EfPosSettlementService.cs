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
    ILowStockNotifier? lowStockNotifier = null) : IPosSettlementService
{
    private const string SettlementOperation = "pos-sale-settle";
    private const string SaleReceiptType = "sale";
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
        if (normalizedRequest is null ||
            normalizedRequest.OrganizationId != saleScope.OrganizationId ||
            !TryValidateSplit(normalizedRequest.Payments, saleScope.CurrencyCode, saleScope.TotalMinorUnits))
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
            await lowStockNotifier.EvaluateProductsAsync(
                saleScope.OrganizationId,
                saleScope.BranchId,
                productIdsToNotify,
                cancellationToken);
        }

        return result;
    }

    private static SettlePosSaleRequest? NormalizeRequest(SettlePosSaleRequest request)
    {
        if (request.Payments is null)
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
            return BillingCommandServiceResult<PosSaleDto>.RequestConflict("version_conflict");
        }

        var response = JsonSerializer.Deserialize<PosSaleDto>(existing.ResponseJson, JsonOptions);
        return response is null
            ? BillingCommandServiceResult<PosSaleDto>.Invalid("Stored idempotent response could not be read.")
            : BillingCommandServiceResult<PosSaleDto>.Ok(response);
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
                return await action();
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
