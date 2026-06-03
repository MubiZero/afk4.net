namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerProfileDto(
    Guid PlayerAccountId,
    string DisplayName,
    string? PhoneNumber,
    bool PhoneVerified,
    string? PreferredLocale,
    bool MarketingOptIn);
