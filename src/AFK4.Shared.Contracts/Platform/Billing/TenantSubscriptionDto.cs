namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record TenantSubscriptionDto(
    Guid TenantSubscriptionId,
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
    DateTimeOffset UpdatedAtUtc);
