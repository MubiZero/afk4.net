namespace AFK4.Shared.Contracts.Billing;

public sealed record RefundLedgerEntryRequest(
    Guid OrganizationId,
    Guid LedgerEntryId,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);
