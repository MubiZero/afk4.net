using System.IO.Pipes;
using System.Text.Json;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

public sealed class NamedPipePlayerShellStateServer(
    IOptions<AgentOptions> options,
    ILogger<NamedPipePlayerShellStateServer> logger) : IPlayerShellStatePublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object syncRoot = new();
    private PlayerShellStateDto? latestState;
    private Task? serverLoopTask;

    public Task PublishAsync(PlayerShellStateDto state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            latestState = state;
            serverLoopTask ??= Task.Run(() => RunServerLoopAsync(lifetime.Token));
        }

        return Task.CompletedTask;
    }

    private async Task RunServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    options.Value.PlayerShellPipeName,
                    PipeDirection.Out,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellationToken);

                PlayerShellStateDto? state;
                lock (syncRoot)
                {
                    state = latestState;
                }

                if (state is null)
                {
                    continue;
                }

                await JsonSerializer.SerializeAsync(pipe, state, JsonOptions, cancellationToken);
                await pipe.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                logger.LogDebug(exception, "Player Shell state publish failed due to pipe I/O.");
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Player Shell state pipe access was denied.");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        try
        {
            serverLoopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
        }

        lifetime.Dispose();
    }
}
