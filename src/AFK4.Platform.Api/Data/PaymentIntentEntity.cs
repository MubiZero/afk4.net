namespace AFK4.Platform.Api.Data;

public sealed class PaymentIntentEntity
{
    public Guid PaymentIntentId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public long AmountMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public string Purpose { get; set; } = "wallet_topup";

    // pending | fulfilled | cancelled | expired
    public string State { get; set; } = "pending";

    // counter (v1) | gateway (future)
    public string Method { get; set; } = "counter";

    // FulfilledByLedgerEntryId is left null (v1): TopUpWalletAsync returns
    // WalletSummaryDto, not the created ledger entry id. The State flip is
    // the idempotency guard.
    public Guid? FulfilledByLedgerEntryId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? FulfilledAtUtc { get; set; }
}
