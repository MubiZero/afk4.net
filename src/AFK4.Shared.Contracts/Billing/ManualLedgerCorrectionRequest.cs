namespace AFK4.Shared.Contracts.Billing;

public sealed record ManualLedgerCorrectionRequest(
    Guid OrganizationId,
    string AccountType,
    MoneyDto Amount,
    int QuantitySeconds,
    string Reason,
    string IdempotencyKey);
