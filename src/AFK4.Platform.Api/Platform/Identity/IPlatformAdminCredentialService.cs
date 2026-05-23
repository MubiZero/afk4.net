using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

public interface IPlatformAdminCredentialService
{
    Task<PlatformAdminSignInResponse?> SignInAsync(PlatformAdminSignInRequest request, CancellationToken cancellationToken);
}
