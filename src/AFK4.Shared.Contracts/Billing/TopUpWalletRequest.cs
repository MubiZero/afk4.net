namespace AFK4.Shared.Contracts.Billing;

public sealed record TopUpWalletRequest(
    Guid OrganizationId,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);
