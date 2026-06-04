namespace AFK4.Shared.Contracts.Players;

public sealed record OperatorTopUpIntentDto(
    Guid PaymentIntentId,
    Guid PlayerAccountId,
    string DisplayName,
    long AmountMinorUnits,
    string CurrencyCode,
    string State,
    string Method,
    DateTimeOffset CreatedAtUtc,
    string? SeatName);
