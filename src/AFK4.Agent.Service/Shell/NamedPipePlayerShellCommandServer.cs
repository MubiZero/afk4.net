using System.IO.Pipes;
using System.Text.Json;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

public sealed class NamedPipePlayerShellCommandServer(
    IOptions<AgentOptions> options,
    IPlayerShellCommandHandler commandHandler,
    ILogger<NamedPipePlayerShellCommandServer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    options.Value.PlayerShellCommandPipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(stoppingToken);
                var command = await JsonSerializer.DeserializeAsync<PlayerShellCommandDto>(
                    pipe,
                    JsonOptions,
                    stoppingToken);

                var result = command is null
                    ? new PlayerShellCommandResultDto(
                        CommandId: Guid.Empty,
                        Status: "Rejected",
                        Message: "Shell command could not be read.",
                        ObservedAtUtc: DateTimeOffset.UtcNow)
                    : await commandHandler.HandleAsync(command, stoppingToken);

                await JsonSerializer.SerializeAsync(pipe, result, JsonOptions, stoppingToken);
                await pipe.FlushAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IOException exception)
            {
                logger.LogDebug(exception, "Player Shell command pipe I/O failed.");
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Player Shell command payload was not valid JSON.");
            }
        }
    }
}
