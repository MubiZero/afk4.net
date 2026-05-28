namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInByTenantKeyRequest(
    string TenantKey,
    string UserName,
    string Password);
