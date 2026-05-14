using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class PlayerShellProcessSupervisorTests
{
    [Fact]
    public async Task EnsureRunningAsync_StartsShellWhenRequiredAndNotRunning()
    {
        using var executable = TemporaryExecutable.Create();
        var processQuery = new RecordingProcessQuery(isRunning: false);
        var processStarter = new RecordingProcessStarter();
        var supervisor = CreateSupervisor(executable.Path, processQuery, processStarter);

        await supervisor.EnsureRunningAsync(AgentRuntimeState.Locked(DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Equal(executable.Path, processStarter.LastExecutablePath);
        Assert.Equal(1, processStarter.StartCount);
    }

    [Fact]
    public async Task EnsureRunningAsync_DoesNotStartDuplicateWhenShellAlreadyRunning()
    {
        using var executable = TemporaryExecutable.Create();
        var processQuery = new RecordingProcessQuery(isRunning: true);
        var processStarter = new RecordingProcessStarter();
        var supervisor = CreateSupervisor(executable.Path, processQuery, processStarter);

        await supervisor.EnsureRunningAsync(AgentRuntimeState.Active(CreateLease(), DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Equal(0, processStarter.StartCount);
    }

    [Fact]
    public async Task EnsureRunningAsync_RestartsShellAfterUnexpectedExit()
    {
        using var executable = TemporaryExecutable.Create();
        var processQuery = new SequenceProcessQuery([true, false]);
        var processStarter = new RecordingProcessStarter();
        var supervisor = CreateSupervisor(executable.Path, processQuery, processStarter);
        var runtimeState = AgentRuntimeState.Active(CreateLease(), DateTimeOffset.UtcNow);

        await supervisor.EnsureRunningAsync(runtimeState, CancellationToken.None);
        await supervisor.EnsureRunningAsync(runtimeState, CancellationToken.None);

        Assert.Equal(1, processStarter.StartCount);
    }

    [Fact]
    public async Task EnsureRunningAsync_DoesNothingWhenExecutablePathMissing()
    {
        var processQuery = new RecordingProcessQuery(isRunning: false);
        var processStarter = new RecordingProcessStarter();
        var supervisor = CreateSupervisor(string.Empty, processQuery, processStarter);

        await supervisor.EnsureRunningAsync(AgentRuntimeState.Locked(DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Equal(0, processStarter.StartCount);
    }

    private static PlayerShellProcessSupervisor CreateSupervisor(
        string executablePath,
        IPlayerShellProcessQuery processQuery,
        IPlayerShellProcessStarter processStarter)
    {
        return new PlayerShellProcessSupervisor(
            Options.Create(new AgentOptions
            {
                PlayerShellExecutablePath = executablePath,
                PlayerShellStartArguments = "--from-test"
            }),
            processQuery,
            processStarter,
            NullLogger<PlayerShellProcessSupervisor>.Instance);
    }

    private static SessionLeaseDto CreateLease()
    {
        return new SessionLeaseDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
    }

    private sealed class RecordingProcessQuery(bool isRunning) : IPlayerShellProcessQuery
    {
        public bool IsRunning(string executablePath)
        {
            return isRunning;
        }
    }

    private sealed class SequenceProcessQuery(IReadOnlyList<bool> values) : IPlayerShellProcessQuery
    {
        private int index;

        public bool IsRunning(string executablePath)
        {
            var value = values[Math.Min(index, values.Count - 1)];
            index++;
            return value;
        }
    }

    private sealed class RecordingProcessStarter : IPlayerShellProcessStarter
    {
        public int StartCount { get; private set; }

        public string? LastExecutablePath { get; private set; }

        public string? LastArguments { get; private set; }

        public void Start(string executablePath, string arguments)
        {
            StartCount++;
            LastExecutablePath = executablePath;
            LastArguments = arguments;
        }
    }

    private sealed class TemporaryExecutable : IDisposable
    {
        private TemporaryExecutable(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryExecutable Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"afk4-player-shell-{Guid.NewGuid():N}.exe");
            File.WriteAllText(path, string.Empty);
            return new TemporaryExecutable(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
