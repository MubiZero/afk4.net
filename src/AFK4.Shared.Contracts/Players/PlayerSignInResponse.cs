namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSignInResponse(
    Guid PlayerAccountId,
    Guid OrganizationId,
    string DisplayName,
    bool PhoneVerified,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
