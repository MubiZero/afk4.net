namespace AFK4.Shared.Contracts.Identity;

public sealed record UpdateStaffUserRolesRequest(
    Guid OrganizationId,
    IReadOnlyList<string> RoleNames);
