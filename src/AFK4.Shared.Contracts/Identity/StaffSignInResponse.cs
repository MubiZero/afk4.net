namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInResponse(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> Permissions)
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];
}
