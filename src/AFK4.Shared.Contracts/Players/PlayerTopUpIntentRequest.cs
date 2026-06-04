namespace AFK4.Shared.Contracts.Players;

// Player requests a wallet top-up.
// CurrencyCode defaults to "TJS" when null or blank.
// Method ∈ { "counter", "dcgate" }; null/blank → "counter" (operator-confirmed at the desk).
public sealed record PlayerTopUpIntentRequest(
    long AmountMinorUnits,
    string? CurrencyCode,
    string? Method = null);
