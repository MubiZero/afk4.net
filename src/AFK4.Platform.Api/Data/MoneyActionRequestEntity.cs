namespace AFK4.Platform.Api.Data;

/// <summary>
/// A high-risk money action held for second-pair-of-eyes approval (anti-fraud spec §5.2). Append-only
/// in spirit: the terminal state is set once (pending → approved/rejected/expired) and the original
/// command is never edited — on approval the verbatim payload is replayed through the existing
/// EfBillingCommandService so the resulting ledger entry carries the usual immutability guarantees.
/// </summary>
public sealed class MoneyActionRequestEntity
{
    public Guid MoneyActionRequestId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ShiftId { get; set; }

    // MoneyActionTypeNames.* — the write-off-classified action type.
    public string ActionType { get; set; } = string.Empty;

    public Guid RequestedByStaffUserId { get; set; }

    // The original command request (amount, currency, reason, target ledger entry / player,
    // idempotency key) so approval can replay it verbatim.
    public string PayloadJson { get; set; } = "{}";

    public long AmountMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public string Reason { get; set; } = string.Empty;

    // MoneyActionRequestStateNames.* — pending → approved / rejected / expired.
    public string State { get; set; } = string.Empty;

    // The second pair of eyes; must differ from RequestedByStaffUserId. Null until decided.
    public Guid? ApprovedByStaffUserId { get; set; }

    public string? DecisionReason { get; set; }

    public Guid? ResultingLedgerEntryId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}
