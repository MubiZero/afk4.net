using System.IO.Pipes;
using System.Text.Json;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class NamedPipePlayerShellStateServerTests
{
    private static readonly TimeSpan PipeObservationTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task PublishAsync_MakesLatestStateAvailableToLaterShellClient()
    {
        var pipeName = $"afk4-test-shell-state-{Guid.NewGuid():N}";
        using var server = CreateServer(pipeName);
        var state = CreateState(PlayerShellStateNames.Active);

        await server.PublishAsync(state, CancellationToken.None);
        await Task.Delay(100);

        var received = await ReadStateAsync(pipeName);

        Assert.NotNull(received);
        Assert.Equal(PlayerShellStateNames.Active, received.State);
        Assert.Equal(state.SessionId, received.SessionId);
    }

    [Fact]
    public async Task PublishAsync_UpdatesStateForNextShellConnection()
    {
        var pipeName = $"afk4-test-shell-state-{Guid.NewGuid():N}";
        using var server = CreateServer(pipeName);

        await server.PublishAsync(CreateState(PlayerShellStateNames.Locked), CancellationToken.None);
        _ = await ReadStateAsync(pipeName);

        var active = CreateState(PlayerShellStateNames.Active);
        await server.PublishAsync(active, CancellationToken.None);
        await Task.Delay(100);

        var received = await ReadStateAsync(pipeName);

        Assert.NotNull(received);
        Assert.Equal(PlayerShellStateNames.Active, received.State);
        Assert.Equal(active.SessionId, received.SessionId);
    }

    private static NamedPipePlayerShellStateServer CreateServer(string pipeName)
    {
        return new NamedPipePlayerShellStateServer(
            Options.Create(new AgentOptions
            {
                PlayerShellPipeName = pipeName
            }),
            NullLogger<NamedPipePlayerShellStateServer>.Instance);
    }

    private static async Task<PlayerShellStateDto?> ReadStateAsync(string pipeName)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.In,
            PipeOptions.Asynchronous);

        using var timeout = new CancellationTokenSource(PipeObservationTimeout);
        await pipe.ConnectAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<PlayerShellStateDto>(
            pipe,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            timeout.Token);
    }

    private static PlayerShellStateDto CreateState(string state)
    {
        return new PlayerShellStateDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("8999549a-e1c1-4b25-8df5-ae8111d8fb97"),
            State: state,
            SessionId: state == PlayerShellStateNames.Active
                ? Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb")
                : null,
            LeaseExpiresAtUtc: state == PlayerShellStateNames.Active
                ? DateTimeOffset.Parse("2026-05-18T06:19:06Z")
                : null,
            RemainingSeconds: state == PlayerShellStateNames.Active ? 900 : null,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: state == PlayerShellStateNames.Active ? "Session is active." : "This PC is locked.",
            LauncherApps: []);
    }
}
