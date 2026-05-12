using AFK4.Operator.App.Realtime;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorRealtimeStartupTests
{
    [Fact]
    public async Task StartAsync_WhenClientStartFails_ReturnsNullAndLogsException()
    {
        var exception = new InvalidOperationException("hub unavailable");
        var startedClient = new ThrowingOperatorRealtimeClient(exception);
        Exception? loggedException = null;
        var startup = new OperatorRealtimeStartup(
            () => startedClient,
            ex => loggedException = ex);

        var client = await startup.StartAsync(CancellationToken.None);

        Assert.Null(client);
        Assert.Same(exception, loggedException);
        Assert.True(startedClient.Disposed);
    }

    private sealed class ThrowingOperatorRealtimeClient : IOperatorRealtimeClient
    {
        private readonly Exception exception;

        public ThrowingOperatorRealtimeClient(Exception exception)
        {
            this.exception = exception;
        }

        public bool Disposed { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw exception;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
