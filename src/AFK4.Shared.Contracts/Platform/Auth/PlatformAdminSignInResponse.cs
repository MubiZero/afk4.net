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
