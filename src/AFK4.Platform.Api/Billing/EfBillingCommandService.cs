using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class EfBillingCommandService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IBillingCommandService
{
    private const string PlayerCreateOperation = "player-create";
    private const string WalletTopUpOperation = "wallet-top-up";
    private const string RefundOperation = "refund";
    private const string ManualCorrectionOperation = "manual-correction";
    private const string DebtPaymentOperation = "debt-payment";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCommandServiceResult<PlayerAccountDto>> CreatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<PlayerAccountDto, CreatePlayerAccountRequest>(
            request.OrganizationId,
            branchId,
            PlayerCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.OrganizationId == Guid.Empty)
        {
            return BillingCommandServiceResult<PlayerAccountDto>.Invalid("Organization id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BillingCommandServiceResult<PlayerAccountDto>.Invalid("Display name is required.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var player = new PlayerAccountEntity
            {
                PlayerAccountId = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                HomeBranchId = branchId,
                DisplayName = request.DisplayName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                IsActive = true,
                CreatedAtUtc = now
            };

            dbContext.PlayerAccounts.Add(player);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToDto(player);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                PlayerCreateOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<PlayerAccountDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<PlayerAccountDto, CreatePlayerAccountRequest>(
            request.OrganizationId,
            branchId,
            PlayerCreateOperation,
            request.IdempotencyKey,
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<WalletSummaryDto>> TopUpWalletAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        TopUpWalletRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<WalletSummaryDto, object>(
            request.OrganizationId,
            branchId,
            WalletTopUpOperation,
            request.IdempotencyKey,
            PlayerScopedRequest(playerAccountId, request),
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var player = await FindPlayerAsync(request.OrganizationId, branchId, playerAccountId, cancellationToken);
        if (player is null)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Missing("Player account was not found.");
        }

        if (request.Amount.MinorUnits <= 0)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Top-up amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.Amount.CurrencyCode))
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Currency code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Reason is required.");
        }

        var currencyValidation = await ValidatePlayerLedgerCurrencyAsync<WalletSummaryDto>(
            playerAccountId,
            request.Amount.CurrencyCode,
            cancellationToken);
        if (currencyValidation is not null)
        {
            return currencyValidation;
        }

        return await ExecuteLedgerSummaryCommandAsync(
            request.OrganizationId,
            branchId,
            WalletTopUpOperation,
            request.IdempotencyKey,
            request,
            PlayerScopedRequest(playerAccountId, request),
            BillingEntryFactory.Create(
                request.OrganizationId,
                branchId,
                playerAccountId,
                sessionId: null,
                playerPackageId: null,
                LedgerEntryTypeNames.TopUp,
                LedgerAccountTypeNames.Wallet,
                request.Amount.MinorUnits,
                quantitySeconds: 0,
                request.Amount.CurrencyCode.Trim().ToUpperInvariant(),
                description: LedgerEntryTypeNames.TopUp,
                request.Reason.Trim(),
                reversesLedgerEntryId: null,
                actorStaffUserId,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public async Task<BillingCommandServiceResult<LedgerEntryDto>> RefundLedgerEntryAsync(
        Guid branchId,
        Guid actorStaffUserId,
        RefundLedgerEntryRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<LedgerEntryDto, RefundLedgerEntryRequest>(
            request.OrganizationId,
            branchId,
            RefundOperation,
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.Amount.MinorUnits <= 0)
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Invalid("Refund amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.Amount.CurrencyCode))
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Invalid("Currency code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Invalid("Reason is required.");
        }

        var original = await dbContext.LedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry =>
                    entry.OrganizationId == request.OrganizationId &&
                    entry.BranchId == branchId &&
                    entry.LedgerEntryId == request.LedgerEntryId,
                cancellationToken);

        if (original is null)
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Missing("Ledger entry was not found.");
        }

        if (!string.Equals(original.CurrencyCode, request.Amount.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Invalid("Refund currency must match the original entry currency.");
        }

        if (original.AmountMinorUnits == 0)
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Invalid("Refund amount cannot exceed the original amount.");
        }

        var originalAmountAbs = Math.Abs(original.AmountMinorUnits);
        var alreadyRefundedAbs = await GetAlreadyRefundedAmountAsync(original.LedgerEntryId, cancellationToken);
        var remainingRefundable = originalAmountAbs - alreadyRefundedAbs;

        if (request.Amount.MinorUnits > remainingRefundable)
        {
            return BillingCommandServiceResult<LedgerEntryDto>.Invalid("Refund amount cannot exceed the remaining refundable amount.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var refundAmount = original.AmountMinorUnits > 0
                ? -request.Amount.MinorUnits
                : request.Amount.MinorUnits;
            var refund = BillingEntryFactory.Create(
                original.OrganizationId,
                original.BranchId,
                original.PlayerAccountId,
                original.SessionId,
                original.PlayerPackageId,
                LedgerEntryTypeNames.Refund,
                original.AccountType,
                refundAmount,
                quantitySeconds: 0,
                original.CurrencyCode,
                description: LedgerEntryTypeNames.Refund,
                request.Reason.Trim(),
                original.LedgerEntryId,
                actorStaffUserId,
                now);

            dbContext.LedgerEntries.Add(refund);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = LedgerBalanceProjector.ToDto(refund);
            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                RefundOperation,
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<LedgerEntryDto>.Ok(response);
        }, () => ReplayIdempotencyAsync<LedgerEntryDto, RefundLedgerEntryRequest>(
            request.OrganizationId,
            branchId,
            RefundOperation,
            request.IdempotencyKey,
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<BillingCommandServiceResult<WalletSummaryDto>> ManualCorrectionAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        ManualLedgerCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<WalletSummaryDto, object>(
            request.OrganizationId,
            branchId,
            ManualCorrectionOperation,
            request.IdempotencyKey,
            PlayerScopedRequest(playerAccountId, request),
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var player = await FindPlayerAsync(request.OrganizationId, branchId, playerAccountId, cancellationToken);
        if (player is null)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Missing("Player account was not found.");
        }

        if (!IsManualCorrectionAccountType(request.AccountType))
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Unsupported ledger account type.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 8)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Manual correction reason must be at least 8 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Amount.CurrencyCode))
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Currency code is required.");
        }

        var currencyValidation = await ValidatePlayerLedgerCurrencyAsync<WalletSummaryDto>(
            playerAccountId,
            request.Amount.CurrencyCode,
            cancellationToken);
        if (currencyValidation is not null)
        {
            return currencyValidation;
        }

        return await ExecuteLedgerSummaryCommandAsync(
            request.OrganizationId,
            branchId,
            ManualCorrectionOperation,
            request.IdempotencyKey,
            request,
            PlayerScopedRequest(playerAccountId, request),
            BillingEntryFactory.Create(
                request.OrganizationId,
                branchId,
                playerAccountId,
                sessionId: null,
                playerPackageId: null,
                LedgerEntryTypeNames.ManualCorrection,
                request.AccountType,
                request.Amount.MinorUnits,
                request.QuantitySeconds,
                request.Amount.CurrencyCode.Trim().ToUpperInvariant(),
                description: LedgerEntryTypeNames.ManualCorrection,
                request.Reason.Trim(),
                reversesLedgerEntryId: null,
                actorStaffUserId,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public async Task<BillingCommandServiceResult<WalletSummaryDto>> PayDebtAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PayDebtRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync<WalletSummaryDto, object>(
            request.OrganizationId,
            branchId,
            DebtPaymentOperation,
            request.IdempotencyKey,
            PlayerScopedRequest(playerAccountId, request),
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        var player = await FindPlayerAsync(request.OrganizationId, branchId, playerAccountId, cancellationToken);
        if (player is null)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Missing("Player account was not found.");
        }

        if (request.Amount.MinorUnits <= 0)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Debt payment amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.Amount.CurrencyCode))
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Currency code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Reason is required.");
        }

        var currencyValidation = await ValidatePlayerLedgerCurrencyAsync<WalletSummaryDto>(
            playerAccountId,
            request.Amount.CurrencyCode,
            cancellationToken);
        if (currencyValidation is not null)
        {
            return currencyValidation;
        }

        var current = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, playerAccountId, cancellationToken);
        if (current is null)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Missing("Player wallet summary was not found.");
        }

        if (current.DebtBalance.MinorUnits < request.Amount.MinorUnits)
        {
            return BillingCommandServiceResult<WalletSummaryDto>.Invalid("Debt payment cannot exceed current debt balance.");
        }

        return await ExecuteLedgerSummaryCommandAsync(
            request.OrganizationId,
            branchId,
            DebtPaymentOperation,
            request.IdempotencyKey,
            request,
            PlayerScopedRequest(playerAccountId, request),
            BillingEntryFactory.Create(
                request.OrganizationId,
                branchId,
                playerAccountId,
                sessionId: null,
                playerPackageId: null,
                LedgerEntryTypeNames.DebtPayment,
                LedgerAccountTypeNames.Debt,
                -request.Amount.MinorUnits,
                quantitySeconds: 0,
                request.Amount.CurrencyCode.Trim().ToUpperInvariant(),
                description: LedgerEntryTypeNames.DebtPayment,
                request.Reason.Trim(),
                reversesLedgerEntryId: null,
                actorStaffUserId,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private async Task<BillingCommandServiceResult<WalletSummaryDto>> ExecuteLedgerSummaryCommandAsync<TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        object requestHashInput,
        LedgerEntryEntity entry,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            dbContext.LedgerEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);

            var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, entry.PlayerAccountId, cancellationToken);
            if (summary is null)
            {
                return BillingCommandServiceResult<WalletSummaryDto>.Missing("Player wallet summary was not found.");
            }

            AddIdempotencyRecord(
                organizationId,
                branchId,
                operation,
                idempotencyKey,
                requestHashInput,
                summary,
                entry.CreatedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);

            return BillingCommandServiceResult<WalletSummaryDto>.Ok(summary);
        }, () => ReplayIdempotencyAsync<WalletSummaryDto, object>(
            organizationId,
            branchId,
            operation,
            idempotencyKey,
            requestHashInput,
            cancellationToken), cancellationToken);
    }

    private async Task<BillingCommandServiceResult<TResponse>?> GetExistingIdempotencyAsync<TResponse, TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BillingCommandServiceResult<TResponse>.Invalid("Idempotency key is required.");
        }

        var requestHash = HashRequest(request);
        var idempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey);
        var existing = await dbContext.BillingCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.Operation == operation &&
                    candidate.IdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return BillingCommandServiceResult<TResponse>.RequestConflict("Idempotency key was already used for a different request.");
        }

        var response = JsonSerializer.Deserialize<TResponse>(existing.ResponseJson, JsonOptions);

        return response is null
            ? BillingCommandServiceResult<TResponse>.Invalid("Stored idempotent response could not be read.")
            : BillingCommandServiceResult<TResponse>.Ok(response);
    }

    private async Task<PlayerAccountEntity?> FindPlayerAsync(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        return await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.HomeBranchId == branchId &&
                    player.PlayerAccountId == playerAccountId,
                cancellationToken);
    }

    private void AddIdempotencyRecord<TRequest, TResponse>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        TResponse response,
        DateTimeOffset now)
    {
        dbContext.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            Operation = operation,
            IdempotencyKeyHash = BillingCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private static PlayerAccountDto ToDto(PlayerAccountEntity player)
    {
        return new PlayerAccountDto(
            player.PlayerAccountId,
            player.OrganizationId,
            player.HomeBranchId,
            player.DisplayName,
            player.PhoneNumber,
            player.IsActive,
            player.CreatedAtUtc);
    }

    private async Task<long> GetAlreadyRefundedAmountAsync(Guid originalLedgerEntryId, CancellationToken cancellationToken)
    {
        var refundAmounts = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.ReversesLedgerEntryId == originalLedgerEntryId &&
                (entry.EntryType == LedgerEntryTypeNames.Refund || entry.EntryType == LedgerEntryTypeNames.Reversal))
            .Select(entry => entry.AmountMinorUnits)
            .ToListAsync(cancellationToken);

        return refundAmounts.Sum(Math.Abs);
    }

    private async Task<BillingCommandServiceResult<TResponse>?> ValidatePlayerLedgerCurrencyAsync<TResponse>(
        Guid playerAccountId,
        string requestedCurrencyCode,
        CancellationToken cancellationToken)
    {
        var normalizedRequestedCurrency = requestedCurrencyCode.Trim();
        var currencies = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (currencies.Count > 1)
        {
            return BillingCommandServiceResult<TResponse>.Invalid("Player ledger contains multiple currencies.");
        }

        var existingCurrency = currencies.SingleOrDefault();
        if (existingCurrency is not null &&
            !string.Equals(existingCurrency, normalizedRequestedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return BillingCommandServiceResult<TResponse>.Invalid("Requested currency must match the player ledger currency.");
        }

        return null;
    }

    private async Task<BillingCommandServiceResult<TResponse>?> ReplayIdempotencyAsync<TResponse, TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return await GetExistingIdempotencyAsync<TResponse, TRequest>(
            organizationId,
            branchId,
            operation,
            idempotencyKey,
            request,
            cancellationToken);
    }

    private static bool IsManualCorrectionAccountType(string accountType)
    {
        return accountType is
            LedgerAccountTypeNames.Wallet or
            LedgerAccountTypeNames.Debt;
    }

    private static string HashRequest<TRequest>(TRequest request)
    {
        return BillingCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
    }

    private static object PlayerScopedRequest<TRequest>(Guid playerAccountId, TRequest request)
    {
        return new
        {
            PlayerAccountId = playerAccountId,
            Request = request
        };
    }

    private async Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        Func<Task<BillingCommandServiceResult<TResponse>?>>? recoverIdempotencyRaceAsync,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            try
            {
                return await action();
            }
            catch (DbUpdateException) when (recoverIdempotencyRaceAsync is not null)
            {
                dbContext.ChangeTracker.Clear();
                var recovered = await recoverIdempotencyRaceAsync();

                if (recovered is not null)
                {
                    return recovered;
                }

                throw;
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (DbUpdateException) when (recoverIdempotencyRaceAsync is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var recovered = await recoverIdempotencyRaceAsync();

            if (recovered is not null)
            {
                return recovered;
            }

            throw;
        }
    }

    private Task<BillingCommandServiceResult<TResponse>> ExecuteInTransactionAsync<TResponse>(
        Func<Task<BillingCommandServiceResult<TResponse>>> action,
        CancellationToken cancellationToken)
    {
        return ExecuteInTransactionAsync(action, recoverIdempotencyRaceAsync: null, cancellationToken);
    }
}
