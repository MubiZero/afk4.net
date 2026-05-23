namespace AFK4.Shared.Contracts.Identity;

public sealed record UpdateStaffUserProfileRequest(
    Guid OrganizationId,
    string UserName,
    string DisplayName);
