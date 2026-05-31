namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record InvoiceListItemDto(
    Guid InvoiceId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    int Number,
    string Kind,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset DueAtUtc,
    long AmountMinorUnits,
    string CurrencyCode,
    string Status);
