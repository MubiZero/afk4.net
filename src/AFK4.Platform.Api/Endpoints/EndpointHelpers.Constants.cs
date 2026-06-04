namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    // Shared credit reason for both the dcgate webhook and the operator fulfil paths.
    // MUST be identical so that a concurrent double-confirm (webhook + operator, or vice-versa)
    // hashes to the same idempotency fingerprint and collapses to a clean replay instead of a
    // RequestConflict that would make the second caller return a spurious error.
    public const string TopUpIntentCreditReason = "wallet top-up via top-up intent";
}
