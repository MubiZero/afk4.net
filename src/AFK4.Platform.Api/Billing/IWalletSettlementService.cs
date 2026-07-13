using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Billing;

public sealed record WalletSettlementResult(bool Succeeded, string? ErrorCode, LedgerEntryEntity? Entry)
{
    public static WalletSettlementResult Ok(LedgerEntryEntity entry) => new(true, null, entry);

    public static WalletSettlementResult Reject(string code) => new(false, code, null);
}

/// <summary>
/// Stages wallet ledger entries within a caller-owned transaction. The caller must use
/// serializable transaction semantics for the affected player wallet and call SaveChangesAsync
/// after all coordinated commerce changes are staged. This service deliberately does neither:
/// cross-context balance and reversal races rely on the caller's transaction isolation.
/// </summary>
public interface IWalletSettlementService
{
    Task<WalletSettlementResult> DebitAsync(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        Guid shiftId,
        long amountMinorUnits,
        string currencyCode,
        string description,
        string reason,
        Guid actorStaffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<WalletSettlementResult> ReverseAsync(
        LedgerEntryEntity originalDebit,
        Guid actorStaffUserId,
        string description,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
