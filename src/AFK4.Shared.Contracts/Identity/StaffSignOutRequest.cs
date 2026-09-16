namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignOutRequest(
    Guid OrganizationId,
    string RefreshToken);
