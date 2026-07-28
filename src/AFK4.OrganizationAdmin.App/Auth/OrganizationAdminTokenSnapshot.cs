namespace AFK4.OrganizationAdmin.App.Auth;

public sealed record OrganizationAdminTokenSnapshot(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];

    public IReadOnlyList<Guid> BranchIds { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];
}
