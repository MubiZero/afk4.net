namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record OrganizationSubscriptionDto(
    Guid OrganizationSubscriptionId,
    Guid OrganizationId,
    string PlanCode,
    string Status,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? NextInvoiceUtc,
    long AmountMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    bool CancelAtPeriodEnd,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? PaymentGraceUntilUtc,
    int? DiscountPercent,
    long? DiscountAmountMinorUnits,
    DateTimeOffset? DiscountUntilUtc,
    string? DiscountReason);
