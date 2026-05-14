using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

public interface IPlayerShellStatePublisher
{
    Task PublishAsync(PlayerShellStateDto state, CancellationToken cancellationToken);
}
