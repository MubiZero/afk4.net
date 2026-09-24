using System.Diagnostics;
using System.IO.Pipes;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Канал на настоящих именованных каналах: на macOS и Linux .NET держит их на Unix-сокетах, так
/// что рукопожатие, проталкивание и запросы проверяются и вне Windows. Список доступа и сессия
/// клиента по словам Windows — только на Windows-дорожке.
/// </summary>
public sealed class ShellPipeServerTests
{
    // Сессия процесса теста. На Windows сервер узнаёт сессию хоста у самой системы, а не из
    // приветствия, поэтому «консолью» в тестах служит настоящая сессия, а чужого хоста изображает
    // консоль в соседней.
    private static readonly int CurrentSession = CurrentProcessSession();
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Hello_IsAnsweredWithTheCurrentStateRightAway()
    {
        await using var harness = await Harness.StartAsync();
        await using var host = await harness.ConnectAsync();

        var frame = await host.ReadAsync();

        Assert.Equal(ShellPipeMessageTypeNames.State, frame.Type);
        Assert.Equal(PlayerShellStateNames.Locked, frame.State!.State);
    }

    [Fact]
    public async Task AnOlderHost_IsToldTheProtocolDoesNotMatch()
    {
        await using var harness = await Harness.StartAsync();
        await using var host = await harness.ConnectAsync(protocol: 1);

        var frame = await host.ReadAsync();

        Assert.Equal(ShellPipeMessageTypeNames.Bye, frame.Type);
        Assert.Equal(ShellPipeErrorCodeNames.ProtocolMismatch, frame.Reason);
        Assert.Null(await host.ReadOrEndAsync());
    }

    [Fact]
    public async Task AHostFromAnotherSession_IsTurnedAway()
    {
        // Удалённый рабочий стол к игровому ПК не должен получать его состояние и запускать игры.
        await using var harness = await Harness.StartAsync(consoleSession: CurrentSession + 1);
        await using var host = await harness.ConnectAsync();

        var frame = await host.ReadAsync();

        Assert.Equal(ShellPipeMessageTypeNames.Bye, frame.Type);
        Assert.Equal(ShellPipeErrorCodeNames.WrongSession, frame.Reason);
    }

    [Fact]
    public async Task AChangedState_ArrivesWithoutBeingAskedFor()
    {
        await using var harness = await Harness.StartAsync();
        await using var host = await harness.ConnectAsync();
        _ = await host.ReadAsync();

        harness.Builder.Current = harness.Builder.Current with { State = PlayerShellStateNames.Active, SessionId = Guid.NewGuid() };
        harness.Signal.Notify();

        var frame = await host.ReadAsync();
        Assert.Equal(PlayerShellStateNames.Active, frame.State!.State);
    }

    [Fact]
    public async Task OnlyTheClockMoving_DoesNotSendAFrame_ButTheKeepAliveDoes()
    {
        // Остаток хост досчитывает сам; кадр ради тиканья — пустая работа на каждом ПК клуба.
        await using var harness = await Harness.StartAsync(keepAlive: TimeSpan.FromMilliseconds(800));
        await using var host = await harness.ConnectAsync();
        var first = await host.ReadAsync();

        harness.Builder.Current = harness.Builder.Current with { RemainingSeconds = 1799, ObservedAtUtc = DateTimeOffset.UtcNow };
        harness.Signal.Notify();

        var started = DateTimeOffset.UtcNow;
        var next = await host.ReadAsync();
        var waited = DateTimeOffset.UtcNow - started;

        Assert.Equal(first.State!.State, next.State!.State);
        Assert.True(waited >= TimeSpan.FromMilliseconds(500), $"A frame arrived after {waited.TotalMilliseconds} ms — before the keep-alive was due.");
    }

    [Fact]
    public async Task ARequest_IsAnsweredWithItsOwnId()
    {
        await using var harness = await Harness.StartAsync();
        await using var host = await harness.ConnectAsync();
        _ = await host.ReadAsync();

        var requestId = Guid.NewGuid();
        await host.WriteAsync(new ShellPipeMessage(
            ShellPipeMessageTypeNames.Request,
            Request: new ShellPipeRequestDto(requestId, ShellPipeRequestTypeNames.Assist, new Dictionary<string, string>())));

        var reply = await host.ReadUntilAsync(ShellPipeMessageTypeNames.Reply);
        Assert.Equal(requestId, reply.Reply!.RequestId);
        Assert.True(reply.Reply.Ok);
        Assert.Equal([ShellPipeRequestTypeNames.Assist], harness.Requests.Types);
    }

    [Fact]
    public async Task AClubCommand_IsForwardedToTheConnectedHost()
    {
        await using var harness = await Harness.StartAsync();
        await using var host = await harness.ConnectAsync();
        _ = await host.ReadAsync();

        var commandId = Guid.NewGuid();
        Assert.True(harness.HostChannel.TryPost(new ShellPipeMessage(
            ShellPipeMessageTypeNames.Command,
            Command: new ShellPipeCommandDto(commandId, "message", "Через пять минут закрываемся"))));

        var frame = await host.ReadUntilAsync(ShellPipeMessageTypeNames.Command);
        Assert.Equal(commandId, frame.Command!.CommandId);
        Assert.Equal("Через пять минут закрываемся", frame.Command.Text);
    }

    [Fact]
    public async Task AfterTheHostLeaves_TheNextHostIsServed()
    {
        // Оболочку перезапускают — обновление, падение WebView2. Канал не должен умереть с ней.
        await using var harness = await Harness.StartAsync();
        await using (var first = await harness.ConnectAsync())
        {
            _ = await first.ReadAsync();
        }

        var frame = await harness.ReadFirstFrameOfANewHostAsync();

        Assert.Equal(ShellPipeMessageTypeNames.State, frame.Type);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ShellPipeServer server;

        private Harness(string pipeName, ShellPipeTimings timings, int consoleSession)
        {
            PipeName = pipeName;
            server = new ShellPipeServer(
                Options.Create(new AgentOptions { ShellPipeName = pipeName }),
                Builder,
                Signal,
                Requests,
                new FixedLaunchContext(consoleSession),
                TimeProvider.System,
                NullLogger<ShellPipeServer>.Instance,
                timings,
                HostChannel);
        }

        public ShellHostChannel HostChannel { get; } = new();

        public string PipeName { get; }

        public StubStateBuilder Builder { get; } = new();

        public ShellStateSignal Signal { get; } = new();

        public RecordingRequestHandler Requests { get; } = new();

        public static async Task<Harness> StartAsync(TimeSpan? keepAlive = null, int? consoleSession = null)
        {
            // Коротко: на macOS канал — это Unix-сокет во временной папке, а путь к нему не длиннее
            // 104 символов.
            var harness = new Harness(
                $"afk4-shell-{Guid.NewGuid():N}"[..23],
                new ShellPipeTimings(
                    HelloTimeout: TimeSpan.FromSeconds(5),
                    RebuildInterval: TimeSpan.FromMilliseconds(50),
                    KeepAliveInterval: keepAlive ?? TimeSpan.FromMinutes(1)),
                consoleSession ?? CurrentSession);
            await harness.server.StartAsync(CancellationToken.None);
            return harness;
        }

        public async Task<HostConnection> ConnectAsync(int protocol = ShellPipeProtocol.Version)
        {
            var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(FrameTimeout);
            await pipe.ConnectAsync(timeout.Token);
            var connection = new HostConnection(pipe);
            await connection.WriteAsync(new ShellPipeMessage(
                ShellPipeMessageTypeNames.Hello,
                Hello: new ShellPipeHelloDto(protocol, "test-host", CurrentSession)));
            return connection;
        }

        /// <summary>
        /// Подключиться так, как это делает хост: канал закрылся до первого кадра — переподключиться.
        /// На Unix новый клиент может успеть встать в очередь старого слушателя, который закроется
        /// вместе с прошлым соединением; на Windows клиент просто ждёт, пока агент откроет канал снова.
        /// </summary>
        public async Task<ShellPipeMessage> ReadFirstFrameOfANewHostAsync()
        {
            for (var attempt = 1; ; attempt++)
            {
                await using var host = await ConnectAsync();
                var frame = await host.ReadOrEndAsync();
                if (frame is not null || attempt == 5)
                {
                    return frame ?? throw new EndOfStreamException("The agent never served a new host.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        public async ValueTask DisposeAsync()
        {
            await server.StopAsync(CancellationToken.None);
            server.Dispose();
        }
    }

    private sealed class HostConnection(NamedPipeClientStream pipe) : IAsyncDisposable
    {
        public Task WriteAsync(ShellPipeMessage message) => ShellPipeCodec.WriteAsync(pipe, message, CancellationToken.None);

        public async Task<ShellPipeMessage> ReadAsync() =>
            await ReadOrEndAsync() ?? throw new EndOfStreamException("The agent closed the pipe.");

        public async Task<ShellPipeMessage?> ReadOrEndAsync()
        {
            using var timeout = new CancellationTokenSource(FrameTimeout);
            return await ShellPipeCodec.ReadAsync(pipe, timeout.Token);
        }

        public async Task<ShellPipeMessage> ReadUntilAsync(string type)
        {
            while (true)
            {
                var message = await ReadAsync();
                if (message.Type == type)
                {
                    return message;
                }
            }
        }

        public ValueTask DisposeAsync() => pipe.DisposeAsync();
    }

    private sealed class StubStateBuilder : IPlayerShellStateBuilder
    {
        public PlayerShellStateDto Current { get; set; } = new(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: 1800,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: []);

        public PlayerShellStateDto Build() => Current;
    }

    private sealed class RecordingRequestHandler : IPlayerShellRequestHandler
    {
        public List<string> Types { get; } = [];

        public Task<ShellPipeReplyDto> HandleAsync(ShellPipeRequestDto request, CancellationToken cancellationToken)
        {
            lock (Types)
            {
                Types.Add(request.Type);
            }

            return Task.FromResult(new ShellPipeReplyDto(request.RequestId, Ok: true));
        }
    }

    private static int CurrentProcessSession()
    {
        using var self = Process.GetCurrentProcess();
        return self.SessionId;
    }

    private sealed class FixedLaunchContext(int sessionId) : IPlayerShellLaunchContext
    {
        public PlayerShellLaunchTarget? GetActiveUserSession() => new(sessionId, IsCurrentProcessSession: false);
    }
}
