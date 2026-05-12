namespace AFK4.Operator.App.Auth;

public sealed record OperatorTokenSnapshot(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
