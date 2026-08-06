using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

public interface IPlatformAdminCredentialService
{
    // Password check only — this issues a sign-in challenge, never a working session. The caller
    // must complete /auth/2fa/setup or /auth/2fa/verify with the returned ChallengeToken to obtain
    // a real PlatformAdminSignInResponse.
    Task<PlatformAdminSignInChallengeResponse?> SignInAsync(PlatformAdminSignInRequest request, CancellationToken cancellationToken);
}
