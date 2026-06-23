using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// Executes a money action through the existing, verified <see cref="IBillingCommandService"/>
/// (anti-fraud spec §5.2). Refunds and manual corrections / debt write-offs are dispatched to their
/// respective commands so ledger immutability, idempotency, currency and refund-cap checks all run
/// as the tested code. For a refund the resulting entry id comes straight back; for a correction
/// (which returns a wallet summary) it is recovered by a best-effort lookup of the freshly written
/// manual-correction entry.
/// </summary>
public sealed class EfMoneyActionExecutor(
    PlatformDbContext dbContext,
    IBillingCommandService billingCommandService) : IMoneyActionExecutor
{
    public async Task<MoneyActionExecutionResult> ExecuteAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        MoneyActionType effectiveType,
        MoneyActionCommand command,
        CancellationToken cancellationToken)
    {
        // Server-side mirror of the operator UI gate: no money movement on a deactivated player —
        // here as well as on the per-player endpoints, so the approval workflow (which replays a
        // held payload on /approve, possibly after the player was deactivated) cannot bypass it.
        var inactiveGuard = await RejectInactivePlayerAsync(organizationId, effectiveType, command, cancellationToken);
        if (inactiveGuard is not null)
        {
            return inactiveGuard;
        }

        if (effectiveType == MoneyActionType.Refund)
        {
            return await ExecuteRefundAsync(organizationId, branchId, actorStaffUserId, command, cancellationToken);
        }

        return await ExecuteCorrectionAsync(organizationId, branchId, actorStaffUserId, command, cancellationToken);
    }

    private async Task<MoneyActionExecutionResult?> RejectInactivePlayerAsync(
        Guid organizationId,
        MoneyActionType effectiveType,
        MoneyActionCommand command,
        CancellationToken cancellationToken)
    {
        // A refund targets the player who owns the ledger entry — resolve from the entry so a spoofed
        // command.PlayerAccountId can't slip a refund past the guard. A correction targets the command's player.
        Guid? targetPlayerAccountId = command.PlayerAccountId == Guid.Empty ? null : command.PlayerAccountId;
        if (effectiveType == MoneyActionType.Refund && command.LedgerEntryId is { } ledgerEntryId)
        {
            targetPlayerAccountId = await dbContext.LedgerEntries
                .AsNoTracking()
                .Where(entry => entry.LedgerEntryId == ledgerEntryId && entry.OrganizationId == organizationId)
                .Select(entry => entry.PlayerAccountId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (targetPlayerAccountId is not { } playerAccountId)
        {
            return null; // No resolvable player — let the billing command surface not-found.
        }

        var isActive = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(player => player.PlayerAccountId == playerAccountId && player.OrganizationId == organizationId)
            .Select(player => (bool?)player.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        return isActive == false
            ? MoneyActionExecutionResult.Invalid("Player account is inactive.")
            : null;
    }

    private async Task<MoneyActionExecutionResult> ExecuteRefundAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        MoneyActionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.LedgerEntryId is not { } ledgerEntryId)
        {
            return MoneyActionExecutionResult.Invalid("A refund requires the target ledger entry id.");
        }

        var request = new RefundLedgerEntryRequest(
            organizationId,
            ledgerEntryId,
            new MoneyDto(command.CurrencyCode, Math.Abs(command.SignedAmountMinorUnits)),
            command.Reason,
            command.IdempotencyKey);

        var result = await billingCommandService.RefundLedgerEntryAsync(branchId, actorStaffUserId, request, cancellationToken);
        return Map(result, result.Response?.LedgerEntryId);
    }

    private async Task<MoneyActionExecutionResult> ExecuteCorrectionAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        MoneyActionCommand command,
        CancellationToken cancellationToken)
    {
        var request = new ManualLedgerCorrectionRequest(
            organizationId,
            command.AccountType,
            new MoneyDto(command.CurrencyCode, command.SignedAmountMinorUnits),
            command.QuantitySeconds,
            command.Reason,
            command.IdempotencyKey);

        var result = await billingCommandService.ManualCorrectionAsync(
            command.PlayerAccountId, branchId, actorStaffUserId, request, cancellationToken);
        if (!result.Succeeded)
        {
            return Map<WalletSummaryDto>(result, resultingLedgerEntryId: null);
        }

        // The manual-correction command returns a wallet summary, not the entry — recover the just
        // written entry's id so the request can link to the ledger.
        var resultingId = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.PlayerAccountId == command.PlayerAccountId
                && entry.CreatedByStaffUserId == actorStaffUserId
                && entry.EntryType == LedgerEntryTypeNames.ManualCorrection)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.LedgerEntryId)
            .Select(entry => (Guid?)entry.LedgerEntryId)
            .FirstOrDefaultAsync(cancellationToken);

        return MoneyActionExecutionResult.Ok(resultingId);
    }

    private static MoneyActionExecutionResult Map<T>(BillingCommandServiceResult<T> result, Guid? resultingLedgerEntryId)
    {
        if (result.Succeeded)
        {
            return MoneyActionExecutionResult.Ok(resultingLedgerEntryId);
        }

        var error = result.Error ?? "Money action failed.";
        if (result.NotFound)
        {
            return MoneyActionExecutionResult.Missing(error);
        }

        return result.Conflict
            ? MoneyActionExecutionResult.RequestConflict(error)
            : MoneyActionExecutionResult.Invalid(error);
    }
}
