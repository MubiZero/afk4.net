using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

public sealed class PlayerShellCommandHandler(
    IProcessPolicyEnforcer processPolicyEnforcer,
    IProcessLauncher processLauncher) : IPlayerShellCommandHandler
{
    public async Task<PlayerShellCommandResultDto> HandleAsync(
        PlayerShellCommandDto command,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(command.Type, "launch-app", StringComparison.Ordinal))
        {
            return Rejected(command.CommandId, $"Unsupported Shell command type '{command.Type}'.");
        }

        if (!command.Payload.TryGetValue("appId", out var appId) || string.IsNullOrWhiteSpace(appId))
        {
            return Rejected(command.CommandId, "Launcher command payload must include appId.");
        }

        var app = processPolicyEnforcer.FindAllowedLauncherApp(appId);
        if (app is null)
        {
            return Rejected(command.CommandId, "App is not allowed on this device.");
        }

        if (!File.Exists(app.ExecutablePath))
        {
            return Rejected(command.CommandId, "Configured app executable does not exist.");
        }

        await processLauncher.LaunchAsync(app.ExecutablePath, app.Arguments, cancellationToken);

        return new PlayerShellCommandResultDto(
            CommandId: command.CommandId,
            Status: "Accepted",
            Message: "Launch request accepted.",
            ObservedAtUtc: DateTimeOffset.UtcNow);
    }

    private static PlayerShellCommandResultDto Rejected(Guid commandId, string message)
    {
        return new PlayerShellCommandResultDto(
            CommandId: commandId,
            Status: "Rejected",
            Message: message,
            ObservedAtUtc: DateTimeOffset.UtcNow);
    }
}
