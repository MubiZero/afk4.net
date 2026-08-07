namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record CreateInvoiceRequest(
    string Kind,
    long AmountMinorUnits,
    string Description,
    DateTimeOffset? DueAtUtc);
