namespace AFK4.Shared.Contracts.Billing;

/// <summary>
/// Submit a high-risk money action through the anti-fraud control layer (§5.2). The guard decides
/// whether it executes now, is held for approval, or is refused on a cap breach. <c>ActionType</c> is
/// <c>refund</c> or <c>manual_correction</c>; a debt-reducing correction is classified as a write-off
/// server-side. <c>SignedAmountMinorUnits</c> is signed (corrections may be ±); refunds use its magnitude.
/// </summary>
public sealed record MoneyActionSubmitRequest(
    Guid OrganizationId,
    string ActionType,
    Guid PlayerAccountId,
    Guid? LedgerEntryId,
    string AccountType,
    long SignedAmountMinorUnits,
    string CurrencyCode,
    int QuantitySeconds,
    string Reason,
    string IdempotencyKey);

/// <summary>Outcome of a submitted money action: <c>executed</c> / <c>pending_approval</c> / <c>rejected</c>.</summary>
public sealed record MoneyActionSubmitResponse(
    string Outcome,
    Guid? ResultingLedgerEntryId,
    Guid? MoneyActionRequestId);

/// <summary>Approve or reject a pending money action; the optional note is recorded on the request.</summary>
public sealed record MoneyActionDecisionRequest(string? DecisionReason);

/// <summary>A pending money action for the Manager Review screen (§5.5).</summary>
public sealed record MoneyActionRequestDto(
    Guid MoneyActionRequestId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ShiftId,
    string ActionType,
    Guid RequestedByStaffUserId,
    long AmountMinorUnits,
    string CurrencyCode,
    string Reason,
    string State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

/// <summary>The pending-approvals feed.</summary>
public sealed record MoneyActionRequestListResponse(IReadOnlyList<MoneyActionRequestDto> Requests);
