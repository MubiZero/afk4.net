namespace AFK4.Shared.Contracts.Players;

// Player-editable profile fields. Both optional; null means "leave unchanged".
public sealed record UpdatePlayerProfileRequest(
    string? PreferredLocale,
    bool? MarketingOptIn);
