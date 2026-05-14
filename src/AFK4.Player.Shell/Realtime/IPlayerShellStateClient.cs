using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Realtime;

public interface IPlayerShellStateClient
{
    IAsyncEnumerable<PlayerShellStateDto> ReadStatesAsync(CancellationToken cancellationToken);
}
