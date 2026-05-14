using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AFK4.Player.Shell.Configuration;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Realtime;

public sealed class NamedPipePlayerShellStateClient(PlayerShellOptions options) : IPlayerShellStateClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<PlayerShellStateDto> ReadStatesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            PlayerShellStateDto? state = null;

            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    options.PipeName,
                    PipeDirection.In,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(options.PipeConnectionTimeoutMilliseconds, cancellationToken);
                state = await JsonSerializer.DeserializeAsync<PlayerShellStateDto>(
                    pipe,
                    JsonOptions,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }

            if (state is not null)
            {
                yield return state;
            }

            await Task.Delay(options.ReconnectDelayMilliseconds, cancellationToken);
        }
    }
}
