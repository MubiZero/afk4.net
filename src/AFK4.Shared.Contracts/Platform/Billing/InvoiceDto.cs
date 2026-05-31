namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record InvoiceDto(
    Guid InvoiceId,
    Guid OrganizationId,
    int Number,
    string Kind,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset DueAtUtc,
    long AmountMinorUnits,
    string CurrencyCode,
    string Status,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? VoidedAtUtc,
    string? VoidReason,
    string Description);
