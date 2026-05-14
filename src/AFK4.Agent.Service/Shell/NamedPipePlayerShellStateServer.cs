using System.IO.Pipes;
using System.Text.Json;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

public sealed class NamedPipePlayerShellStateServer(
    IOptions<AgentOptions> options,
    ILogger<NamedPipePlayerShellStateServer> logger) : IPlayerShellStatePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(PlayerShellStateDto state, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, options.Value.PlayerShellPipeConnectionTimeoutMilliseconds)));

        try
        {
            await using var pipe = new NamedPipeServerStream(
                options.Value.PlayerShellPipeName,
                PipeDirection.Out,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await pipe.WaitForConnectionAsync(timeout.Token);
            await JsonSerializer.SerializeAsync(pipe, state, JsonOptions, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Player Shell state publish skipped because no Shell client connected in time.");
        }
        catch (IOException exception)
        {
            logger.LogDebug(exception, "Player Shell state publish failed due to pipe I/O.");
        }
    }
}
