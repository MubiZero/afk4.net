namespace AFK4.Shared.Contracts.Payments;

public sealed record CreateDcTopUpRequest(
    Guid PlayerAccountId,
    long AmountMinorUnits,
    string? CurrencyCode);

public sealed record DcTopUpDto(
    Guid IntentId,
    string PayUrl,
    string Comment,
    long AmountMinorUnits,
    string CurrencyCode,
    string CardLast4);
