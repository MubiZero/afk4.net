using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public interface IStaffCredentialService
{
    Task<StaffSignInResponse?> SignInAsync(StaffSignInRequest request, CancellationToken cancellationToken);

    Task<StaffSignInResponse?> SignInByTenantKeyAsync(
        StaffSignInByTenantKeyRequest request,
        CancellationToken cancellationToken);
}
