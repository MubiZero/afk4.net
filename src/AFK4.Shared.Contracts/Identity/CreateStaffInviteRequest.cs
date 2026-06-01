namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Invite a new staff member by email; they set their own password on accept. Additive to
/// inline-password creation (<see cref="CreateStaffUserRequest"/>) — it does not replace it.
/// </summary>
public sealed record CreateStaffInviteRequest(
    Guid OrganizationId,
    string UserName,
    string DisplayName,
    string Email,
    IReadOnlyList<string> RoleNames);
