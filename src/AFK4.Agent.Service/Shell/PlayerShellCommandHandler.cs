using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

public sealed class PlayerShellCommandHandler(
    IProcessPolicyEnforcer processPolicyEnforcer,
    IProcessLauncher processLauncher,
    IAssistanceRequestReporter assistanceRequestReporter,
    TimeProvider timeProvider,
    ILogger<PlayerShellCommandHandler> logger) : IPlayerShellCommandHandler
{
    public async Task<PlayerShellCommandResultDto> HandleAsync(
        PlayerShellCommandDto command,
        CancellationToken cancellationToken)
    {
        // «Позвать оператора» уходит на сервер ключом устройства: на запертом экране сессии нет,
        // и назвать себя игроку нечем — а машина известна всегда.
        if (string.Equals(command.Type, "call-operator", StringComparison.Ordinal))
        {
            try
            {
                await assistanceRequestReporter.ReportAsync(timeProvider.GetUtcNow(), cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Оболочке важно знать правду: если стойка о вызове не узнала, показывать
                // «оператор идёт» нельзя.
                logger.LogWarning(exception, "Call-operator request did not reach the platform.");
                return Rejected(command.CommandId, "The counter could not be reached.");
            }

            return new PlayerShellCommandResultDto(
                CommandId: command.CommandId,
                Status: "Accepted",
                Message: "The counter has been called.",
                ObservedAtUtc: timeProvider.GetUtcNow());
        }

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
