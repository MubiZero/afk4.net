using System.IO.Pipes;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Realtime;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Realtime;

/// <summary>
/// Хостовая сторона канала против поддельного агента на настоящем именованном канале. Проверку
/// «сервер в сессии 0» здесь выключают: поддельный агент живёт в процессе теста.
/// </summary>
public sealed class ShellPipeClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task IntroducesItselfAndDeliversTheStateTheAgentPushes()
    {
        await using var agent = FakeAgent.Start();
        var (client, run, lifetime) = StartClient(agent.PipeName);

        var hello = await agent.AcceptAsync();
        Assert.Equal(ShellPipeProtocol.Version, hello.Protocol);

        await agent.SendAsync(new ShellPipeMessage(ShellPipeMessageTypeNames.State, State: State(PlayerShellStateNames.Active)));

        await using var states = client.ReadStatesAsync(lifetime.Token).GetAsyncEnumerator();
        Assert.True(await states.MoveNextAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(PlayerShellStateNames.Active, states.Current.State);

        await lifetime.CancelAsync();
        await run;
    }

    [Fact]
    public async Task ARequestIsAnsweredByTheReplyWithItsId()
    {
        await using var agent = FakeAgent.Start();
        var (client, run, lifetime) = StartClient(agent.PipeName);
        await agent.AcceptAsync();
        await agent.SendAsync(new ShellPipeMessage(ShellPipeMessageTypeNames.State, State: State(PlayerShellStateNames.Active)));
        await WaitUntilAsync(() => client.IsConnected);

        var replyTask = client.RequestAsync(
            ShellPipeRequestTypeNames.Launch,
            new Dictionary<string, string> { ["appId"] = "cs2" },
            CancellationToken.None);
        var request = (await agent.ReadAsync()).Request!;
        await agent.SendAsync(new ShellPipeMessage(
            ShellPipeMessageTypeNames.Reply,
            Reply: new ShellPipeReplyDto(request.RequestId, Ok: false, ShellPipeErrorCodeNames.NoSession)));

        var reply = await replyTask.WaitAsync(Timeout);
        Assert.Equal(ShellPipeRequestTypeNames.Launch, request.Type);
        Assert.Equal("cs2", request.Payload["appId"]);
        Assert.Equal(ShellPipeErrorCodeNames.NoSession, reply.ErrorCode);

        await lifetime.CancelAsync();
        await run;
    }

    [Fact]
    public async Task ASignInAndAClubCommand_AreDeliveredInOrder_WithoutBeingAskedFor()
    {
        await using var agent = FakeAgent.Start();
        var (client, run, lifetime) = StartClient(agent.PipeName);
        await agent.AcceptAsync();
        await agent.SendAsync(new ShellPipeMessage(ShellPipeMessageTypeNames.State, State: State(PlayerShellStateNames.Locked)));

        await agent.SendAsync(new ShellPipeMessage(ShellPipeMessageTypeNames.Auth, Auth: Session()));
        await agent.SendAsync(new ShellPipeMessage(
            ShellPipeMessageTypeNames.Command,
            Command: new ShellPipeCommandDto(Guid.NewGuid(), "sign-out")));

        await using var pushes = client.ReadPushesAsync(lifetime.Token).GetAsyncEnumerator();
        Assert.True(await pushes.MoveNextAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal("access-token", pushes.Current.Auth!.AccessToken);
        Assert.True(await pushes.MoveNextAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal("sign-out", pushes.Current.Command!.Type);

        await lifetime.CancelAsync();
        await run;
    }

    [Fact]
    public async Task WithoutTheAgent_ARequestSaysSoAtOnce()
    {
        var client = new ShellPipeClient(Options($"afk4-none-{Guid.NewGuid():N}"[..20]), verifyAgentSession: false);

        var reply = await client.RequestAsync(ShellPipeRequestTypeNames.Assist, new Dictionary<string, string>(), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.AgentUnavailable, reply.ErrorCode);
    }

    [Fact]
    public async Task WhenTheAgentGoesAway_APendingRequestIsAnsweredNotLeftHanging()
    {
        await using var agent = FakeAgent.Start();
        var (client, run, lifetime) = StartClient(agent.PipeName);
        await agent.AcceptAsync();
        await agent.SendAsync(new ShellPipeMessage(ShellPipeMessageTypeNames.State, State: State(PlayerShellStateNames.Locked)));
        await WaitUntilAsync(() => client.IsConnected);

        var replyTask = client.RequestAsync(ShellPipeRequestTypeNames.Assist, new Dictionary<string, string>(), CancellationToken.None);
        _ = await agent.ReadAsync();
        await agent.DisposeAsync();

        var reply = await replyTask.WaitAsync(Timeout);
        Assert.Equal(ShellPipeErrorCodeNames.AgentUnavailable, reply.ErrorCode);

        await lifetime.CancelAsync();
        await run;
    }

    private static (ShellPipeClient Client, Task Run, CancellationTokenSource Lifetime) StartClient(string pipeName)
    {
        var client = new ShellPipeClient(Options(pipeName), verifyAgentSession: false);
        var lifetime = new CancellationTokenSource();
        return (client, client.RunAsync(lifetime.Token), lifetime);
    }

    private static PlayerShellOptions Options(string pipeName) => new()
    {
        ShellPipeName = pipeName,
        ConnectTimeoutMilliseconds = 200,
        RequestTimeoutMilliseconds = 5_000
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(20);
        }
    }

    private static AFK4.Shared.Contracts.Identity.PlatformPersonSessionResponse Session() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Фарход", true, "access-token", DateTimeOffset.UtcNow.AddMinutes(15),
        "refresh-token", DateTimeOffset.UtcNow.AddHours(12), Guid.NewGuid(), "ru", true);

    private static PlayerShellStateDto State(string state) => new(
        OrganizationId: Guid.NewGuid(),
        BranchId: Guid.NewGuid(),
        DeviceId: Guid.NewGuid(),
        State: state,
        SessionId: null,
        LeaseExpiresAtUtc: null,
        RemainingSeconds: null,
        IsOnline: true,
        IsGraceMode: false,
        WarningThresholdSeconds: 300,
        Message: state,
        LauncherApps: []);

    private sealed class FakeAgent : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;
        private bool disposed;

        private FakeAgent(string pipeName)
        {
            PipeName = pipeName;
            pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        public string PipeName { get; }

        public static FakeAgent Start() => new($"afk4-host-{Guid.NewGuid():N}"[..22]);

        public async Task<ShellPipeHelloDto> AcceptAsync()
        {
            using var timeout = new CancellationTokenSource(Timeout);
            await pipe.WaitForConnectionAsync(timeout.Token);
            var hello = await ReadAsync();
            return hello.Hello ?? throw new InvalidDataException($"Expected hello, got {hello.Type}.");
        }

        public async Task<ShellPipeMessage> ReadAsync()
        {
            using var timeout = new CancellationTokenSource(Timeout);
            return await ShellPipeCodec.ReadAsync(pipe, timeout.Token)
                ?? throw new EndOfStreamException("The host closed the pipe.");
        }

        public Task SendAsync(ShellPipeMessage message) => ShellPipeCodec.WriteAsync(pipe, message, CancellationToken.None);

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            await pipe.DisposeAsync();
        }
    }
}
