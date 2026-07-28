namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record SubscriptionListItemDto(
    Guid OrganizationSubscriptionId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string PlanCode,
    string Status,
    string BillingInterval,
    long AmountMinorUnits,
    string CurrencyCode,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? NextInvoiceUtc,
    bool CancelAtPeriodEnd);
