using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class PlayerShellCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_LaunchApp_LaunchesAllowedConfiguredApp()
    {
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(executable.Path, launcher);

        var result = await handler.HandleAsync(CreateLaunchCommand("counter-strike-2"), CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(executable.Path, launcher.LastExecutablePath);
        Assert.Equal("--safe-mode", launcher.LastArguments);
    }

    [Fact]
    public async Task HandleAsync_LaunchApp_RejectsUnknownApp()
    {
        using var executable = TemporaryExecutable.Create();
        var launcher = new RecordingProcessLauncher();
        var handler = CreateHandler(executable.Path, launcher);

        var result = await handler.HandleAsync(CreateLaunchCommand("unknown"), CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(0, launcher.LaunchCount);
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

    private static PlayerShellCommandHandler CreateHandler(string executablePath, IProcessLauncher processLauncher)
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
                        IsEnabled = true
                    }
                ]
            }),
            new RecordingProcessTerminator(),
            NullLogger<ProcessPolicyEnforcer>.Instance);

        return new PlayerShellCommandHandler(enforcer, processLauncher);
    }

    private static PlayerShellCommandDto CreateLaunchCommand(string appId)
    {
        return new PlayerShellCommandDto(
            CommandId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            Type: "launch-app",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["appId"] = appId
            });
    }

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
}
