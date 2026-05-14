using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Launcher;

public sealed class NoOpLauncherCommandClient : ILauncherCommandClient
{
    public static NoOpLauncherCommandClient Instance { get; } = new();

    public Task<PlayerShellCommandResultDto> LaunchAsync(string appId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PlayerShellCommandResultDto(
            CommandId: Guid.NewGuid(),
            Status: "Rejected",
            Message: "Launcher command channel is not connected.",
            ObservedAtUtc: DateTimeOffset.UtcNow));
    }
}
