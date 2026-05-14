using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using AFK4.Player.Shell.Configuration;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Launcher;

public sealed class LauncherCommandClient(PlayerShellOptions options) : ILauncherCommandClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PlayerShellCommandResultDto> LaunchAsync(
        string appId,
        CancellationToken cancellationToken)
    {
        var command = new PlayerShellCommandDto(
            CommandId: Guid.NewGuid(),
            Type: "launch-app",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: new Dictionary<string, string>
            {
                ["appId"] = appId
            });

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                options.CommandPipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(options.PipeConnectionTimeoutMilliseconds, cancellationToken);
            await JsonSerializer.SerializeAsync(pipe, command, JsonOptions, cancellationToken);
            await pipe.FlushAsync(cancellationToken);

            var result = await JsonSerializer.DeserializeAsync<PlayerShellCommandResultDto>(
                pipe,
                JsonOptions,
                cancellationToken);

            return result ?? Rejected(command.CommandId, "Agent returned an empty launcher response.");
        }
        catch (TimeoutException)
        {
            return Rejected(command.CommandId, "Agent launcher command channel is not available.");
        }
        catch (IOException)
        {
            return Rejected(command.CommandId, "Agent launcher command channel failed.");
        }
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
