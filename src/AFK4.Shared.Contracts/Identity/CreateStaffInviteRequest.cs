namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Invite a new staff member by email; they set their own password on accept. This is the sole
/// staff-onboarding path (inline-password creation has been removed).
/// </summary>
public sealed record CreateStaffInviteRequest(
    Guid OrganizationId,
    string UserName,
    string DisplayName,
    string Email,
    IReadOnlyList<string> RoleNames);
