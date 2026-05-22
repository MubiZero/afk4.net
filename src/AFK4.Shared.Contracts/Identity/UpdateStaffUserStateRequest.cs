namespace AFK4.Shared.Contracts.Identity;

public sealed record UpdateStaffUserStateRequest(
    Guid OrganizationId,
    bool IsActive);
