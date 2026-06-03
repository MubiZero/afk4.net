using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

public interface IPlayerTokenService
{
    Task<PlayerSignInResponse> IssueAsync(
        PlayerAccountEntity account, bool phoneVerified, CancellationToken cancellationToken);

    Task<PlayerSignInResponse?> RefreshAsync(
        PlayerRefreshRequest request, CancellationToken cancellationToken);

    Task<PlayerContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);
}
