namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffUserDto(
    Guid StaffUserId,
    Guid OrganizationId,
    string UserName,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> RoleNames,
    DateTimeOffset CreatedAtUtc);
