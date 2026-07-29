using AFK4.Agent.Service;
using AFK4.Agent.Service.Shell;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class OrganizationAdminProcessLauncherTests
{
    [Fact]
    public async Task LaunchPendingAsync_StartsInInteractiveSessionExactlyOnce()
    {
        using var files = new TemporaryFiles();
        var starter = new RecordingStarter();
        var launcher = CreateLauncher(files, starter, isRunning: false);
        await launcher.ScheduleAfterRestartAsync(Instruction(), CancellationToken.None);

        var first = await launcher.LaunchPendingAsync(CancellationToken.None);
        var second = await launcher.LaunchPendingAsync(CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, starter.Count);
        Assert.Equal(7, starter.Target?.SessionId);
        Assert.Equal("--restored", starter.Arguments);
    }

    [Fact]
    public async Task LaunchPendingAsync_WhenStartFails_RetainsPendingLaunchForRetry()
    {
        using var files = new TemporaryFiles();
        var failing = new RecordingStarter { Failure = new InvalidOperationException("start failed") };
        var launcher = CreateLauncher(files, failing, isRunning: false);
        await launcher.ScheduleAfterRestartAsync(Instruction(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => launcher.LaunchPendingAsync(CancellationToken.None));
        var retry = CreateLauncher(files, new RecordingStarter(), isRunning: false);

        Assert.True(await retry.LaunchPendingAsync(CancellationToken.None));
    }

    private static OrganizationAdminProcessLauncher CreateLauncher(TemporaryFiles files, RecordingStarter starter, bool isRunning) =>
        new(Options.Create(new AgentOptions
        {
            UpdateStateDirectory = files.Directory,
            OrganizationAdminExecutablePath = files.Executable,
            OrganizationAdminStartArguments = "--restored"
        }), new FixedContext(), starter, new FixedQuery(isRunning));

    private static ComponentUpdateInstructionDto Instruction() => new(
        Guid.NewGuid(), Guid.NewGuid(), UpdateComponentNames.OrganizationAdmin, "1.2.3", "stable",
        "https://updates.test/admin.msi", "sha", "sig", "ed25519", 1, "notes");

    private sealed class FixedContext : IPlayerShellLaunchContext
    {
        public PlayerShellLaunchTarget? GetActiveUserSession() => new(7, false);
    }

    private sealed class FixedQuery(bool value) : IPlayerShellProcessQuery
    {
        public bool IsRunning(string executablePath, int sessionId) => value;
    }

    private sealed class RecordingStarter : IPlayerShellProcessStarter
    {
        public int Count { get; private set; }
        public string? Arguments { get; private set; }
        public PlayerShellLaunchTarget? Target { get; private set; }
        public Exception? Failure { get; init; }
        public void Start(string executablePath, string arguments, PlayerShellLaunchTarget launchTarget)
        {
            Count++;
            Arguments = arguments;
            Target = launchTarget;
            if (Failure is not null) throw Failure;
        }
    }

    private sealed class TemporaryFiles : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), $"afk4-admin-launch-{Guid.NewGuid():N}");
        public string Executable { get; }
        public TemporaryFiles()
        {
            System.IO.Directory.CreateDirectory(Directory);
            Executable = Path.Combine(Directory, "AFK4.OrganizationAdmin.App.exe");
            File.WriteAllText(Executable, "test");
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
