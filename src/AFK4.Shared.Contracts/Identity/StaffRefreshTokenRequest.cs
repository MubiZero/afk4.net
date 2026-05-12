namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffRefreshTokenRequest(
    Guid OrganizationId,
    string RefreshToken);
