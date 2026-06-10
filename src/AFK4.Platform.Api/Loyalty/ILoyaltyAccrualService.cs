using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Loyalty;

public interface ILoyaltyAccrualService
{
    /// <summary>
    /// Builds (does not persist) a cashback wallet ledger entry for a successful source event,
    /// or null if the org has the source disabled, has no settings row, or the cashback rounds to zero.
    /// The caller adds the returned entry to its own unit of work so the credit is atomic with the source.
    /// </summary>
    Task<LedgerEntryEntity?> BuildCashbackEntryAsync(
        LoyaltyAccrualSource source,
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        long sourceMinorUnits,
        string currencyCode,
        string reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);
}
