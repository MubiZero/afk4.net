using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

public interface IPlayerCredentialService
{
    Task<PlayerSignInResponse?> SignInAsync(PlayerSignInRequest request, CancellationToken cancellationToken);

    /// <summary>Operator-set initial PIN/password for a player (creates the credential row if absent).</summary>
    Task SetPasswordAsync(Guid playerAccountId, string password, CancellationToken cancellationToken);
}
