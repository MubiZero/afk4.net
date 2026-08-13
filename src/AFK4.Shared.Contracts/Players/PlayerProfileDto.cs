namespace AFK4.Shared.Contracts.Players;

// HomeBranchId is what lets the app ask for the club's price list at all: the catalog endpoints are
// per-branch, and until now the player had no way to learn which branch the account belongs to —
// the server resolved it silently on every write. The name comes along so the app can say where it
// is booking without a second round-trip.
public sealed record PlayerProfileDto(
    Guid PlayerAccountId,
    string DisplayName,
    string? PhoneNumber,
    bool PhoneVerified,
    string? PreferredLocale,
    bool MarketingOptIn,
    Guid? HomeBranchId = null,
    string? HomeBranchName = null);
