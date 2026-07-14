using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Commerce;

public sealed class EfShopCommerceCoordinator(
    PlatformDbContext dbContext,
    IShopOrderWorkflow workflow,
    IShopPosSettlementService settlementService,
    TimeProvider timeProvider,
    IShopOrderNotifier notifier,
    ILogger<EfShopCommerceCoordinator> logger,
    IPosService? posService = null) : IShopCommerceCoordinator
{
    private const string PlaceOperation = "shop-order-place";
    private const string RefundOperation = "pos-sale-refund";
    private const int MaxSerializationAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId,
        PlaceShopOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return ShopOrderActionResult.Business("idempotency_key_required");
        }

        var canonicalLines = CanonicalizeLines(request.Lines);
        if (canonicalLines is null)
        {
            return ShopOrderActionResult.Business("invalid_lines");
        }

        var requestHash = HashRequest(new { PlayerAccountId = playerAccountId, Lines = canonicalLines });
        var durableReplay = await GetPlayerShopOrderReplayAsync(
            playerAccountId,
            request.IdempotencyKey,
            requestHash,
            cancellationToken);
        if (durableReplay is not null)
        {
            return durableReplay;
        }

        for (var attempt = 1; attempt <= MaxSerializationAttempts; attempt++)
        {
            try
            {
                var outcome = await ExecutePlacementAttemptAsync(
                    playerAccountId,
                    request.IdempotencyKey,
                    canonicalLines,
                    cancellationToken);
                if (outcome.Notify && outcome.Result.Succeeded)
                {
                    await NotifyCreatedAsync(outcome.Result.Order!, cancellationToken);
                }

                return outcome.Result;
            }
            catch (Exception exception) when (IsSerializationFailure(exception) && attempt < MaxSerializationAttempts)
            {
                dbContext.ChangeTracker.Clear();
                logger.LogWarning(exception, "Retrying serialized shop placement attempt {Attempt}.", attempt + 1);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                dbContext.ChangeTracker.Clear();
                var rejection = await RecheckPlacementRejectionAsync(
                    playerAccountId,
                    request.IdempotencyKey,
                    canonicalLines,
                    cancellationToken);
                if (rejection is not null)
                {
                    return rejection;
                }

                throw;
            }
        }

        throw new InvalidOperationException("Shop placement retry loop terminated unexpectedly.");
    }

    public Task<ShopOrderActionResult> CancelByOperatorAsync(
        Guid branchId,
        Guid orderId,
        Guid staffUserId,
        int? expectedVersion,
        CancellationToken cancellationToken) =>
        CancelAsync(
            () => workflow.ResolveOperatorCancellationAsync(branchId, orderId, expectedVersion, cancellationToken),
            staffUserId,
            cancellationToken);

    public Task<ShopOrderActionResult> CancelByPlayerAsync(
        Guid playerAccountId,
        Guid orderId,
        CancellationToken cancellationToken) =>
        CancelAsync(
            () => workflow.ResolvePlayerCancellationAsync(playerAccountId, orderId, cancellationToken),
            SystemActorIds.PlayerShop,
            cancellationToken);

    public async Task<BillingCommandServiceResult<PosSaleDto>> RefundLinkedSaleAsync(
        Guid saleId,
        Guid staffUserId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return BillingCommandServiceResult<PosSaleDto>.Invalid("idempotency_key_required");
        }

        var linkedOrder = await workflow.ResolveLinkedSaleAsync(saleId, cancellationToken);
        if (!linkedOrder.Succeeded || linkedOrder.Order is null)
        {
            return posService is null
                ? BillingCommandServiceResult<PosSaleDto>.Missing("sale_not_found")
                : await posService.RefundSaleAsync(saleId, staffUserId, request, cancellationToken);
        }
        dbContext.Entry(linkedOrder.Order).State = EntityState.Detached;

        ShopOrderDto? notification = null;
        var result = await ExecuteTransitionWithConflictTranslationAsync(
            () => ExecuteWithSerializationRetriesAsync(() => ExecuteSerializableAsync(async () =>
        {
            notification = null;
            var linked = await workflow.ResolveLinkedSaleAsync(saleId, cancellationToken);
            if (!linked.Succeeded || linked.Order is null)
            {
                return UnitResult<BillingCommandServiceResult<PosSaleDto>>.Uncommitted(
                    BillingCommandServiceResult<PosSaleDto>.Missing("sale_not_found"));
            }

            var order = linked.Order;
            var requestHashInput = new { PosSaleId = saleId, Request = request };
            var replay = await GetIdempotentAsync<PosSaleDto>(
                order.OrganizationId,
                order.BranchId,
                RefundOperation,
                request.IdempotencyKey,
                HashRequest(requestHashInput),
                cancellationToken);
            if (replay is not null)
            {
                return UnitResult<BillingCommandServiceResult<PosSaleDto>>.Uncommitted(replay);
            }

            if (request.OrganizationId != order.OrganizationId)
            {
                return UnitResult<BillingCommandServiceResult<PosSaleDto>>.Uncommitted(
                    BillingCommandServiceResult<PosSaleDto>.Invalid("organization_mismatch"));
            }

            var now = timeProvider.GetUtcNow();
            var settlement = await settlementService.RefundPaidWalletSaleAsync(
                new ShopPosRefundRequest(
                    saleId,
                    order.ShopOrderId,
                    order.WalletLedgerEntryId,
                    staffUserId,
                    request.Reason,
                    now),
                cancellationToken);
            if (!settlement.Succeeded || settlement.Sale is null || settlement.Receipt is null)
            {
                ClearStagedChanges();
                return UnitResult<BillingCommandServiceResult<PosSaleDto>>.Uncommitted(
                    BillingCommandServiceResult<PosSaleDto>.Invalid(settlement.ErrorCode ?? "refund_failed"));
            }

            notification = await workflow.MarkCancelledAsync(
                order,
                staffUserId,
                now,
                cancellationToken);
            var response = PosSaleProjection.ToDto(
                settlement.Sale,
                settlement.Lines,
                settlement.Receipt,
                order.ShopOrderId);
            AddIdempotencyRecord(
                order.OrganizationId,
                order.BranchId,
                RefundOperation,
                request.IdempotencyKey,
                HashRequest(requestHashInput),
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult<BillingCommandServiceResult<PosSaleDto>>.Committed(
                BillingCommandServiceResult<PosSaleDto>.Ok(response));
        }, cancellationToken, async () =>
        {
            notification = null;
            var linked = await workflow.ResolveLinkedSaleAsync(saleId, cancellationToken);
            if (!linked.Succeeded || linked.Order is null) return null;
            return await GetIdempotentAsync<PosSaleDto>(
                linked.Order.OrganizationId,
                linked.Order.BranchId,
                RefundOperation,
                request.IdempotencyKey,
                HashRequest(new { PosSaleId = saleId, Request = request }),
                cancellationToken);
        }), cancellationToken),
            async () =>
            {
                notification = null;
                return (await workflow.ResolveLinkedSaleAsync(saleId, cancellationToken)).Order?.Version;
            },
            _ => BillingCommandServiceResult<PosSaleDto>.RequestConflict("version_conflict"));

        if (notification is not null)
        {
            await NotifyUpdatedAsync(notification, cancellationToken);
        }

        return result;
    }

    private async Task<PlacementOutcome> ExecutePlacementAttemptAsync(
        Guid playerAccountId,
        string idempotencyKey,
        IReadOnlyList<ShopOrderLineInput> canonicalLines,
        CancellationToken cancellationToken)
    {
        return await ExecuteSerializableAsync(async () =>
        {
            var placement = await workflow.ResolvePlacementContextAsync(playerAccountId, cancellationToken);
            if (!placement.Succeeded || placement.Context is null)
            {
                return UnitResult<PlacementOutcome>.Uncommitted(
                    new PlacementOutcome(ShopOrderActionResult.Business(placement.ErrorCode ?? "placement_context_invalid"), false));
            }

            var context = placement.Context;
            var requestHash = HashRequest(new { PlayerAccountId = playerAccountId, Lines = canonicalLines });
            var replay = await GetShopOrderReplayAsync(
                context.OrganizationId,
                context.BranchId,
                idempotencyKey,
                requestHash,
                cancellationToken);
            if (replay is not null)
            {
                return UnitResult<PlacementOutcome>.Uncommitted(new PlacementOutcome(replay, false));
            }

            var now = timeProvider.GetUtcNow();
            var settlement = await settlementService.CreatePaidWalletSaleAsync(
                new ShopPosSaleRequest(
                    context.OrganizationId,
                    context.BranchId,
                    context.PlayerAccountId,
                    context.SessionId,
                    SystemActorIds.PlayerShop,
                    canonicalLines,
                    $"shop-order-place:{BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey)}",
                    now),
                cancellationToken);
            if (!settlement.Succeeded)
            {
                ClearStagedChanges();
                return UnitResult<PlacementOutcome>.Uncommitted(
                    new PlacementOutcome(ShopOrderActionResult.Business(settlement.ErrorCode ?? "settlement_failed"), false));
            }

            var order = await workflow.CreatePlacedAsync(context, settlement, now, cancellationToken);
            AddIdempotencyRecord(
                context.OrganizationId,
                context.BranchId,
                PlaceOperation,
                idempotencyKey,
                requestHash,
                order,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult<PlacementOutcome>.Committed(
                new PlacementOutcome(ShopOrderActionResult.Ok(order), true));
        }, cancellationToken, async () =>
        {
            var placement = await workflow.ResolvePlacementContextAsync(playerAccountId, cancellationToken);
            if (!placement.Succeeded || placement.Context is null) return null;
            var requestHash = HashRequest(new { PlayerAccountId = playerAccountId, Lines = canonicalLines });
            var recovered = await GetShopOrderReplayAsync(
                placement.Context.OrganizationId,
                placement.Context.BranchId,
                idempotencyKey,
                requestHash,
                cancellationToken);
            return recovered is null ? null : new PlacementOutcome(recovered, false);
        });
    }

    private async Task<ShopOrderActionResult> CancelAsync(
        Func<Task<ShopCancellationContextResult>> resolve,
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        ShopOrderDto? notification = null;
        var result = await ExecuteTransitionWithConflictTranslationAsync(
            () => ExecuteWithSerializationRetriesAsync(() => ExecuteSerializableAsync(async () =>
        {
            notification = null;
            var cancellation = await resolve();
            if (!cancellation.Succeeded || cancellation.Order is null)
            {
                var rejected = cancellation.NotFound
                    ? ShopOrderActionResult.Missing()
                    : cancellation.Conflict
                        ? ShopOrderActionResult.VersionConflict(cancellation.CurrentVersion)
                        : ShopOrderActionResult.Business(cancellation.ErrorCode ?? "invalid_transition");
                return UnitResult<ShopOrderActionResult>.Uncommitted(rejected);
            }

            var order = cancellation.Order;
            if (order.Status == ShopOrderStatusNames.Cancelled)
            {
                return UnitResult<ShopOrderActionResult>.Uncommitted(
                    ShopOrderActionResult.Ok(await workflow.ProjectAsync(order, cancellationToken)));
            }

            ShopOrderActionResult cancelled;
            if (order.PosSaleId is Guid saleId)
            {
                var settlement = await settlementService.RefundPaidWalletSaleAsync(
                    new ShopPosRefundRequest(
                        saleId,
                        order.ShopOrderId,
                        order.WalletLedgerEntryId,
                        staffUserId,
                        $"shop-order-cancel:{order.ShopOrderId:D}",
                        timeProvider.GetUtcNow()),
                    cancellationToken);
                if (!settlement.Succeeded)
                {
                    ClearStagedChanges();
                    return UnitResult<ShopOrderActionResult>.Uncommitted(
                        ShopOrderActionResult.Business(settlement.ErrorCode ?? "refund_failed"));
                }

                var dto = await workflow.MarkCancelledAsync(
                    order, staffUserId, timeProvider.GetUtcNow(), cancellationToken);
                cancelled = ShopOrderActionResult.Ok(dto);
            }
            else
            {
                cancelled = await workflow.CancelLegacyAsync(order, staffUserId, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            notification = cancelled.Order;
            return UnitResult<ShopOrderActionResult>.Committed(cancelled);
        }, cancellationToken), cancellationToken),
            async () =>
            {
                notification = null;
                var current = await resolve();
                return current.CurrentVersion ?? current.Order?.Version;
            },
            ShopOrderActionResult.VersionConflict);

        if (notification is not null)
        {
            await NotifyUpdatedAsync(notification, cancellationToken);
        }

        return result;
    }

    private async Task<TResult> ExecuteTransitionWithConflictTranslationAsync<TResult>(
        Func<Task<TResult>> operation,
        Func<Task<int?>> resolveCurrentVersion,
        Func<int?, TResult> createConflict)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (
            exception is DbUpdateConcurrencyException || IsSerializationFailure(exception))
        {
            dbContext.ChangeTracker.Clear();
            return createConflict(await resolveCurrentVersion());
        }
    }

    private async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<Task<UnitResult<TResult>>> action,
        CancellationToken cancellationToken,
        Func<Task<TResult?>>? recoverUniqueRace = null)
        where TResult : class
    {
        if (!dbContext.Database.IsRelational())
        {
            try
            {
                var unit = await action();
                if (!unit.ShouldCommit) ClearStagedChanges();
                return unit.Result;
            }
            catch (DbUpdateException) when (recoverUniqueRace is not null)
            {
                dbContext.ChangeTracker.Clear();
                var recovered = await recoverUniqueRace();
                if (recovered is not null) return recovered;
                throw;
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var unit = await action();
            if (unit.ShouldCommit)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
                ClearStagedChanges();
            }

            return unit.Result;
        }
        catch (DbUpdateException) when (recoverUniqueRace is not null)
        {
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            var recovered = await recoverUniqueRace();
            if (recovered is not null) return recovered;
            throw;
        }
        catch
        {
            await RelationalFailureClassifier.RollbackIfActiveAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<TResult> ExecuteWithSerializationRetriesAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSerializationAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception exception) when (
                IsSerializationFailure(exception) && attempt < MaxSerializationAttempts)
            {
                dbContext.ChangeTracker.Clear();
                logger.LogWarning(
                    exception,
                    "Retrying serialized shop commerce attempt {Attempt}.",
                    attempt + 1);
            }
        }

        throw new InvalidOperationException("Shop commerce retry loop terminated unexpectedly.");
    }

    private async Task<ShopOrderActionResult?> RecheckPlacementRejectionAsync(
        Guid playerAccountId,
        string idempotencyKey,
        IReadOnlyList<ShopOrderLineInput> canonicalLines,
        CancellationToken cancellationToken)
    {
        var placement = await workflow.ResolvePlacementContextAsync(playerAccountId, cancellationToken);
        if (!placement.Succeeded || placement.Context is null)
        {
            return ShopOrderActionResult.Business(placement.ErrorCode ?? "placement_context_invalid");
        }

        var context = placement.Context;
        var replay = await GetShopOrderReplayAsync(
            context.OrganizationId,
            context.BranchId,
            idempotencyKey,
            HashRequest(new { PlayerAccountId = playerAccountId, Lines = canonicalLines }),
            cancellationToken);
        if (replay is not null) return replay;

        var settlement = await settlementService.CreatePaidWalletSaleAsync(
            new ShopPosSaleRequest(
                context.OrganizationId,
                context.BranchId,
                context.PlayerAccountId,
                context.SessionId,
                SystemActorIds.PlayerShop,
                canonicalLines,
                $"shop-order-place:{BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey)}",
                timeProvider.GetUtcNow()),
            cancellationToken);
        ClearStagedChanges();
        return settlement.Succeeded
            ? null
            : ShopOrderActionResult.Business(settlement.ErrorCode ?? "settlement_failed");
    }

    private async Task<ShopOrderActionResult?> GetShopOrderReplayAsync(
        Guid organizationId,
        Guid branchId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BillingCommandIdempotency.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.Operation == PlaceOperation &&
                candidate.IdempotencyKeyHash == BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
                cancellationToken);
        if (existing is null) return null;
        if (existing.RequestHash != requestHash)
        {
            return ShopOrderActionResult.VersionConflict(null) with { ErrorCode = "idempotency_conflict" };
        }

        var response = JsonSerializer.Deserialize<ShopOrderDto>(existing.ResponseJson, JsonOptions);
        return response is null
            ? ShopOrderActionResult.Business("idempotency_response_invalid")
            : ShopOrderActionResult.Ok(response);
    }

    private async Task<ShopOrderActionResult?> GetPlayerShopOrderReplayAsync(
        Guid playerAccountId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.BillingCommandIdempotency.AsNoTracking()
            .Where(candidate =>
                candidate.Operation == PlaceOperation &&
                candidate.IdempotencyKeyHash == BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey))
            .ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            var response = JsonSerializer.Deserialize<ShopOrderDto>(candidate.ResponseJson, JsonOptions);
            if (response?.PlayerAccountId != playerAccountId) continue;
            if (candidate.RequestHash != requestHash)
            {
                return ShopOrderActionResult.VersionConflict(null) with { ErrorCode = "idempotency_conflict" };
            }

            return ShopOrderActionResult.Ok(response);
        }

        return null;
    }

    private async Task<BillingCommandServiceResult<T>?> GetIdempotentAsync<T>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BillingCommandIdempotency.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.Operation == operation &&
                candidate.IdempotencyKeyHash == BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
                cancellationToken);
        if (existing is null) return null;
        if (existing.RequestHash != requestHash)
        {
            return BillingCommandServiceResult<T>.RequestConflict("idempotency_conflict");
        }

        var response = JsonSerializer.Deserialize<T>(existing.ResponseJson, JsonOptions);
        return response is null
            ? BillingCommandServiceResult<T>.Invalid("idempotency_response_invalid")
            : BillingCommandServiceResult<T>.Ok(response);
    }

    private void AddIdempotencyRecord<T>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        string requestHash,
        T response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = operation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = requestHash,
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private void ClearStagedChanges()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            entry.State = entry.State == EntityState.Added ? EntityState.Detached : EntityState.Unchanged;
        }
    }

    private async Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.NotifyCreatedAsync(order, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish created shop order {ShopOrderId}.", order.Id);
        }
    }

    private async Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.NotifyUpdatedAsync(order, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to publish updated shop order {ShopOrderId}.", order.Id);
        }
    }

    private static IReadOnlyList<ShopOrderLineInput>? CanonicalizeLines(IReadOnlyList<ShopOrderLineInput> lines)
    {
        if (lines.Count == 0 || lines.Any(line => line.ProductId == Guid.Empty || line.Quantity <= 0)) return null;
        try
        {
            return lines
                .GroupBy(line => line.ProductId)
                .Select(group => new ShopOrderLineInput(
                    group.Key,
                    group.Aggregate(0, (quantity, line) => checked(quantity + line.Quantity))))
                .OrderBy(line => line.ProductId)
                .ToList();
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
        exception.InnerException is not null && IsSerializationFailure(exception.InnerException);

    private static string HashRequest<T>(T request) =>
        BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));

    private sealed record PlacementOutcome(ShopOrderActionResult Result, bool Notify);
    private sealed record UnitResult<TResult>(TResult Result, bool ShouldCommit)
    {
        public static UnitResult<TResult> Committed(TResult result) => new(result, true);
        public static UnitResult<TResult> Uncommitted(TResult result) => new(result, false);
    }
}
