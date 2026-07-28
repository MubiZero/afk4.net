namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInByOrganizationKeyRequest(
    string OrganizationKey,
    string UserName,
    string Password);
