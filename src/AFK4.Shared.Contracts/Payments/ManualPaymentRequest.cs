using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Payments;

public sealed record ManualPaymentRequest(
    Guid OrganizationId,
    string PaymentMethod,
    MoneyDto Amount,
    string Note,
    string IdempotencyKey);
