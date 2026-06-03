namespace AFK4.Shared.Contracts.Players;

// Player requests a wallet top-up at the counter.
// CurrencyCode defaults to "TJS" when null or blank.
public sealed record PlayerTopUpIntentRequest(
    long AmountMinorUnits,
    string? CurrencyCode);
