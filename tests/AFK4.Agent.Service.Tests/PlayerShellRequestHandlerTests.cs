using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class PlayerShellRequestHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T20:00:00Z");

    [Fact]
    public async Task Launch_DuringASession_StartsTheConfiguredApp()
    {
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(executable.Path, launcher, SessionRunning());

        var reply = await handler.HandleAsync(LaunchRequest("counter-strike-2"), CancellationToken.None);

        Assert.True(reply.Ok);
        Assert.Equal(LaunchRequestId, reply.RequestId);
        Assert.Equal(executable.Path, launcher.LastExecutablePath);
        Assert.Equal("--safe-mode", launcher.LastArguments);
    }

    [Fact]
    public async Task Launch_OnALockedPc_IsRefused()
    {
        // Игра на запертом ПК — бесплатное время. Раньше это проверял только экран оболочки.
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(executable.Path, launcher, Locked());

        var reply = await handler.HandleAsync(LaunchRequest("counter-strike-2"), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.NoSession, reply.ErrorCode);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task Launch_OnALockedPc_StartsAnAppTheClubAllowedWithoutASession()
    {
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(executable.Path, launcher, Locked(), allowWithoutSession: true);

        var reply = await handler.HandleAsync(LaunchRequest("counter-strike-2"), CancellationToken.None);

        Assert.True(reply.Ok);
        Assert.Equal(1, launcher.LaunchCount);
    }

    [Fact]
    public async Task Launch_InGrace_StillStartsTheApp()
    {
        // Сеть упала, но сессия оплачена и идёт по аренде — играть можно.
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var runtime = new PlayerShellStateBuilderTests.MemoryRuntimeStateStore(
            AgentRuntimeState.Grace(Guid.NewGuid(), Now.AddMinutes(10), Now));
        var handler = CreateHandler(executable.Path, launcher, runtime);

        var reply = await handler.HandleAsync(LaunchRequest("counter-strike-2"), CancellationToken.None);

        Assert.True(reply.Ok);
    }

    [Fact]
    public async Task Launch_OfAnAppNotOnTheList_IsRefused()
    {
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(executable.Path, launcher, SessionRunning());

        var reply = await handler.HandleAsync(LaunchRequest("unknown"), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.AppNotAllowed, reply.ErrorCode);
        Assert.Equal(0, launcher.LaunchCount);
    }

    [Fact]
    public async Task Launch_WhenTheExecutableIsGone_SaysItIsMissing()
    {
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(
            Path.Combine(Path.GetTempPath(), $"afk4-missing-{Guid.NewGuid():N}.exe"),
            launcher,
            SessionRunning());

        var reply = await handler.HandleAsync(LaunchRequest("counter-strike-2"), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.AppMissing, reply.ErrorCode);
    }

    [Fact]
    public async Task Launch_WithoutAnAppId_IsAnInvalidPayload()
    {
        var handler = CreateHandler("unused", new RecordingProcessLauncher(), SessionRunning());

        var reply = await handler.HandleAsync(Request(ShellPipeRequestTypeNames.Launch), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.InvalidPayload, reply.ErrorCode);
    }

    [Fact]
    public async Task Launch_WhenWindowsRefusesToStartIt_SaysSo()
    {
        using var executable = TemporaryExecutable.Create();
        var handler = CreateHandler(executable.Path, new FailingProcessLauncher(), SessionRunning());

        var reply = await handler.HandleAsync(LaunchRequest("counter-strike-2"), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.LaunchFailed, reply.ErrorCode);
    }

    [Fact]
    public async Task UnknownRequest_IsNamedAsSuch()
    {
        var handler = CreateHandler("unused", new RecordingProcessLauncher(), Locked());

        var reply = await handler.HandleAsync(Request("pause"), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.UnknownRequest, reply.ErrorCode);
    }

    // «Позвать администратора» обязано доехать до стойки: раньше мост оболочки отвечал
    // {requested:true} и не звал никого.
    [Fact]
    public async Task Assist_TellsThePlatform()
    {
        var assistance = new RecordingAssistanceReporter();
        var handler = CreateHandler("unused", new RecordingProcessLauncher(), Locked(), assistance: assistance);

        var reply = await handler.HandleAsync(Request(ShellPipeRequestTypeNames.Assist), CancellationToken.None);

        Assert.True(reply.Ok);
        Assert.Equal(1, assistance.Count);
    }

    // Не дошло до сервера — оболочке нельзя рисовать «администратор идёт».
    [Fact]
    public async Task Assist_WhenThePlatformIsUnreachable_IsRefused()
    {
        var handler = CreateHandler(
            "unused",
            new RecordingProcessLauncher(),
            Locked(),
            assistance: new FailingAssistanceReporter());

        var reply = await handler.HandleAsync(Request(ShellPipeRequestTypeNames.Assist), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(ShellPipeErrorCodeNames.PlatformUnreachable, reply.ErrorCode);
    }

    [Fact]
    public async Task EnforceAsync_TerminatesConfiguredDeniedProcesses()
    {
        var terminator = new RecordingProcessTerminator();
        var enforcer = new ProcessPolicyEnforcer(
            Options.Create(new AgentOptions
            {
                DeniedProcessNames = ["notepad", "taskmgr"]
            }),
            terminator,
            NullLogger<ProcessPolicyEnforcer>.Instance);

        await enforcer.EnforceAsync(CancellationToken.None);

        Assert.Equal(["notepad", "taskmgr"], terminator.ProcessNames);
    }

    private static readonly Guid LaunchRequestId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");

    private static PlayerShellRequestHandler CreateHandler(
        string executablePath,
        IProcessLauncher processLauncher,
        IAgentRuntimeStateStore runtimeState,
        bool allowWithoutSession = false,
        IAssistanceRequestReporter? assistance = null)
    {
        var enforcer = new ProcessPolicyEnforcer(
            Options.Create(new AgentOptions
            {
                LauncherApps =
                [
                    new AgentLauncherAppOptions
                    {
                        AppId = "counter-strike-2",
                        DisplayName = "Counter-Strike 2",
                        ExecutablePath = executablePath,
                        Arguments = "--safe-mode",
                        IsEnabled = true,
                        AllowWithoutSession = allowWithoutSession
                    }
                ]
            }),
            new RecordingProcessTerminator(),
            NullLogger<ProcessPolicyEnforcer>.Instance);

        return new PlayerShellRequestHandler(
            enforcer,
            processLauncher,
            runtimeState,
            assistance ?? new RecordingAssistanceReporter(),
            TimeProvider.System,
            NullLogger<PlayerShellRequestHandler>.Instance);
    }

    private static IAgentRuntimeStateStore Locked() =>
        new PlayerShellStateBuilderTests.MemoryRuntimeStateStore(AgentRuntimeState.Locked(Now));

    private static IAgentRuntimeStateStore SessionRunning()
    {
        var lease = new SessionLeaseDto(
            SessionId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            SeatId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: "active",
            Sequence: 1,
            IssuedAtUtc: Now.AddHours(-1),
            ExpiresAtUtc: Now.AddHours(1),
            SignatureAlgorithm: "none",
            Signature: "test");
        return new PlayerShellStateBuilderTests.MemoryRuntimeStateStore(AgentRuntimeState.Active(lease, Now));
    }

    private static ShellPipeRequestDto LaunchRequest(string appId) =>
        new(
            LaunchRequestId,
            ShellPipeRequestTypeNames.Launch,
            new Dictionary<string, string> { [PlayerShellRequestHandler.AppIdPayloadKey] = appId });

    private static ShellPipeRequestDto Request(string type) =>
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), type, new Dictionary<string, string>());

    private sealed class RecordingProcessLauncher : IProcessLauncher
    {
        public int LaunchCount { get; private set; }

        public string? LastExecutablePath { get; private set; }

        public string? LastArguments { get; private set; }

        public Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken)
        {
            LaunchCount++;
            LastExecutablePath = executablePath;
            LastArguments = arguments;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingProcessLauncher : IProcessLauncher
    {
        public Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken) =>
            throw new System.ComponentModel.Win32Exception(5, "Access is denied.");
    }

    private sealed class RecordingProcessTerminator : IRunningProcessTerminator
    {
        public List<string> ProcessNames { get; } = [];

        public int TerminateByName(string processName)
        {
            ProcessNames.Add(processName);
            return 1;
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
                $"afk4-launcher-{Guid.NewGuid():N}.exe");
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

    private sealed class RecordingAssistanceReporter : IAssistanceRequestReporter
    {
        public int Count { get; private set; }

        public Task ReportAsync(DateTimeOffset requestedAtUtc, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingAssistanceReporter : IAssistanceRequestReporter
    {
        public Task ReportAsync(DateTimeOffset requestedAtUtc, CancellationToken cancellationToken) =>
            throw new HttpRequestException("platform unreachable");
    }
}
