namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInResponse(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> Permissions);
