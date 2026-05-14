using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Launcher;

public interface ILauncherCommandClient
{
    Task<PlayerShellCommandResultDto> LaunchAsync(string appId, CancellationToken cancellationToken);
}
