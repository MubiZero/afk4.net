using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

public interface IPlayerShellCommandHandler
{
    Task<PlayerShellCommandResultDto> HandleAsync(
        PlayerShellCommandDto command,
        CancellationToken cancellationToken);
}
