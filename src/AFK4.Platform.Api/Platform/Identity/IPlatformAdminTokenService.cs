using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

public interface IPlatformAdminTokenService
{
    Task<PlatformAdminSignInResponse> IssueAsync(PlatformAdminUserEntity user, CancellationToken cancellationToken);

    Task<PlatformAdminSignInResponse?> RefreshAsync(PlatformAdminRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<PlatformAdminContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(PlatformAdminSignOutRequest request, CancellationToken cancellationToken);
}
