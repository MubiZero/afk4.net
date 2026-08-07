namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record UpdateSubscriptionRequest(
    string? PlanCode,
    string? BillingInterval,
    string? Status,
    bool? CancelAtPeriodEnd,
    long? AmountMinorUnits,
    DateTimeOffset? CurrentPeriodEndUtc,
    DateTimeOffset? PaymentGraceUntilUtc,
    bool? ClearPaymentGrace = null,
    int? DiscountPercent = null,
    long? DiscountAmountMinorUnits = null,
    DateTimeOffset? DiscountUntilUtc = null,
    string? DiscountReason = null,
    bool? ClearDiscount = null);
