namespace AFK4.Shared.Contracts.Identity;

public sealed record CreateStaffUserRequest(
    Guid OrganizationId,
    string UserName,
    string DisplayName,
    string Password,
    IReadOnlyList<string> RoleNames);
