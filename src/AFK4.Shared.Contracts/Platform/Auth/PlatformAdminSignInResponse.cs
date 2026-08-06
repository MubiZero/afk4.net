namespace AFK4.Shared.Contracts.Platform.Auth;

public sealed record PlatformAdminSignInResponse(
    Guid PlatformAdminId,
    string UserName,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

// First step of sign-in: password alone no longer issues a working session. The caller must present
// this challenge token to one of the /auth/2fa/* routes (setup or verify) to receive the real
// PlatformAdminSignInResponse above. The token is short-lived and opaque — it authorizes nothing
// except those 2FA routes.
public sealed record PlatformAdminSignInChallengeResponse(
    string ChallengeToken,
    DateTimeOffset ExpiresAtUtc,
    bool TwoFactorConfigured);
