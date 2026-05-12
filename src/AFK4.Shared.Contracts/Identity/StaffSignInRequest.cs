namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInRequest(
    Guid OrganizationId,
    string UserName,
    string Password);
