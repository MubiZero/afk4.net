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

    // counter | eskhata | dc
    public string Method { get; set; } = "counter";

    // FulfilledByLedgerEntryId is left null (v1): TopUpWalletAsync returns
    // WalletSummaryDto, not the created ledger entry id. Double-credit is guarded by
    // the billing idempotency key (the intent id), not by this column.
    public Guid? FulfilledByLedgerEntryId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? FulfilledAtUtc { get; set; }

    // --- dcgate (online self-top-up) ---
    // Set only when Method == "dcgate"; null on the counter path.

    // dcgate's own payment id (returned by POST /api/payments). Used for status polls.
    public string? GatewayPaymentId { get; set; }

    // DC pay link the shell renders as a QR (pay.dc.tj/...).
    public string? GatewayPayUrl { get; set; }

    // 18-char DC reference comment dcgate matches incoming bank messages against.
    public string? GatewayComment { get; set; }

    public DateTimeOffset? GatewayExpiresAtUtc { get; set; }

    // payment.disputed webhook flips this true and leaves State == "pending" for the
    // operator to resolve manually; the player money is NOT credited on a dispute.
    public bool Disputed { get; set; }

    // --- eskhata (orderTypeId=3) ---
    // Текст Единого QR из ответа create (для киоска/ПК).
    public string? GatewayQrPayload { get; set; }

    // posId, назначенный банком (нужен для последующих status/cancel/refund).
    public int? GatewayPosId { get; set; }
}
