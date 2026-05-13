namespace AFK4.Shared.Contracts.Billing;

public sealed record PayDebtRequest(
    Guid OrganizationId,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);
