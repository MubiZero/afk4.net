namespace AFK4.Shared.Contracts.Loyalty;

public sealed record CashbackEntryDto(
    long AmountMinorUnits,
    string CurrencyCode,
    string Reason,
    DateTimeOffset CreatedAtUtc);
