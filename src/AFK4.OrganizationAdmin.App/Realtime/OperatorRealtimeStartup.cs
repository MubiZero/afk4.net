using System.Diagnostics;

namespace AFK4.OrganizationAdmin.App.Realtime;

public sealed class OperatorRealtimeStartup(
    Func<IOperatorRealtimeClient> clientFactory,
    Action<Exception>? logException = null)
{
    public async Task<IOperatorRealtimeClient?> StartAsync(CancellationToken cancellationToken)
    {
        IOperatorRealtimeClient? client = null;

        try
        {
            client = clientFactory();
            await client.StartAsync(cancellationToken);
            return client;
        }
        catch (Exception exception)
        {
            if (logException is not null)
            {
                logException(exception);
            }
            else
            {
                Trace.WriteLine(exception);
            }

            if (client is not null)
            {
                await client.DisposeAsync();
            }

            return null;
        }
    }
}
