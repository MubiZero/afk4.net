using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class SafeUpdateInstallerTests
{
    [Fact]
    public async Task InstallAsync_WhenExecutorSucceedsMarksInstalledAndSchedulesRestartForAgentUpdate()
    {
        var instruction = CreateInstruction(UpdateComponentNames.AgentService);
        var artifact = CreateArtifact(instruction);
        var store = new RecordingUpdateInstallStateStore();
        var executor = new RecordingInstallExecutor(UpdateInstallResult.Success("installed"));
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rolled back"));
        var restart = new RecordingRestartScheduler(UpdateRestartResult.Scheduled("restart scheduled"));
        var installer = new SafeUpdateInstaller(
            store,
            executor,
            rollback,
            restart,
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")));

        var result = await installer.InstallAsync(instruction, artifact, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(
            [UpdateStatusNames.Installing, UpdateStatusNames.Installing, UpdateStatusNames.Installed],
            store.States.Select(state => state.Status));
        Assert.Single(executor.ExecutedPlans);
        Assert.Empty(rollback.RolledBackPlans);
        Assert.Single(restart.ScheduledInstructions);
    }

    [Fact]
    public async Task InstallAsync_WhenExecutorFailsRollsBackToTheLastVersionThatWorked()
    {
        var instruction = CreateInstruction(UpdateComponentNames.PlayerShell);
        var artifact = CreateArtifact(instruction);
        // Пакет предыдущей версии лежит на диске — только на него и можно откатиться.
        var previousPackage = Path.Combine(Path.GetTempPath(), $"afk4-previous-{Guid.NewGuid():N}.msi");
        await File.WriteAllTextAsync(previousPackage, "previous");
        var store = new RecordingUpdateInstallStateStore
        {
            LastKnownGood = new LastKnownGoodUpdate(
                UpdateComponentNames.PlayerShell,
                "1.0.0",
                previousPackage,
                DateTimeOffset.Parse("2026-05-01T00:00:00Z"))
        };
        var executor = new RecordingInstallExecutor(UpdateInstallResult.Failed("install failed"));
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var restart = new RecordingRestartScheduler(UpdateRestartResult.NotRequired("restart not required"));
        var installer = new SafeUpdateInstaller(
            store,
            executor,
            rollback,
            restart,
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")));

        try
        {
            var result = await installer.InstallAsync(instruction, artifact, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(
                [UpdateStatusNames.Installing, UpdateStatusNames.Installing, UpdateStatusNames.RollbackStarted, UpdateStatusNames.RolledBack],
                store.States.Select(state => state.Status));
            // Откат идёт предыдущим пакетом, а не тем же самым, который только что не установился.
            var plan = Assert.Single(rollback.RolledBackPlans);
            Assert.Equal(previousPackage, plan.ArtifactPath);
            Assert.Equal("1.0.0", plan.TargetVersion);
            Assert.Contains("install failed", result.Message, StringComparison.Ordinal);
            Assert.Contains("rollback complete", result.Message, StringComparison.Ordinal);
            Assert.Empty(restart.ScheduledInstructions);
        }
        finally
        {
            File.Delete(previousPackage);
        }
    }

    // Откатываться некуда: предыдущего пакета на машине нет. Раньше в этом месте запускался тот же
    // самый пакет, который только что не установился, и это называлось «откатом» — в журнале
    // появлялось RolledBack там, где не откатывалось ничего.
    [Fact]
    public async Task InstallAsync_WithoutAPreviousPackage_SaysRollbackIsImpossibleInsteadOfReinstalling()
    {
        var instruction = CreateInstruction(UpdateComponentNames.PlayerShell);
        var artifact = CreateArtifact(instruction);
        var store = new RecordingUpdateInstallStateStore();
        var executor = new RecordingInstallExecutor(UpdateInstallResult.Failed("install failed"));
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var restart = new RecordingRestartScheduler(UpdateRestartResult.NotRequired("restart not required"));
        var installer = new SafeUpdateInstaller(
            store,
            executor,
            rollback,
            restart,
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")));

        var result = await installer.InstallAsync(instruction, artifact, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(rollback.RolledBackPlans);
        Assert.Equal(UpdateStatusNames.Failed, store.States[^1].Status);
        Assert.Contains("no previously installed package", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Удачная установка запоминает свой пакет: именно он станет целью отката для следующей версии.
    [Fact]
    public async Task InstallAsync_WhenInstallSucceeds_RemembersThePackageAsLastKnownGood()
    {
        var instruction = CreateInstruction(UpdateComponentNames.PlayerShell);
        var artifact = CreateArtifact(instruction);
        var store = new RecordingUpdateInstallStateStore();
        var installer = new SafeUpdateInstaller(
            store,
            new RecordingInstallExecutor(UpdateInstallResult.Success("installed")),
            new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete")),
            new RecordingRestartScheduler(UpdateRestartResult.NotRequired("restart not required")),
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")));

        await installer.InstallAsync(instruction, artifact, CancellationToken.None);

        Assert.NotNull(store.LastKnownGood);
        Assert.Equal(instruction.Version, store.LastKnownGood!.Version);
        Assert.Equal(artifact.FilePath, store.LastKnownGood.ArtifactPath);
    }

    [Fact]
    public async Task InstallAsync_ForOrganizationAdmin_SchedulesRelaunchOnlyAfterSuccessfulInstall()
    {
        var instruction = CreateInstruction(UpdateComponentNames.OrganizationAdmin);
        var launcher = new RecordingOrganizationAdminProcessLauncher();
        var installer = new SafeUpdateInstaller(
            new RecordingUpdateInstallStateStore(),
            new RecordingInstallExecutor(UpdateInstallResult.Success("installed")),
            new RecordingRollbackExecutor(UpdateRollbackResult.Success("rolled back")),
            new RecordingRestartScheduler(UpdateRestartResult.Scheduled("restart scheduled")),
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")),
            launcher);

        var result = await installer.InstallAsync(instruction, CreateArtifact(instruction), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(instruction, Assert.Single(launcher.Scheduled));
    }

    [Fact]
    public async Task InstallAsync_WhenOrganizationAdminInstallFails_DoesNotScheduleRelaunch()
    {
        var instruction = CreateInstruction(UpdateComponentNames.OrganizationAdmin);
        var launcher = new RecordingOrganizationAdminProcessLauncher();
        var installer = new SafeUpdateInstaller(
            new RecordingUpdateInstallStateStore(),
            new RecordingInstallExecutor(UpdateInstallResult.Failed("install failed")),
            new RecordingRollbackExecutor(UpdateRollbackResult.Success("rolled back")),
            new RecordingRestartScheduler(UpdateRestartResult.Scheduled("restart scheduled")),
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")),
            launcher);

        var result = await installer.InstallAsync(instruction, CreateArtifact(instruction), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(launcher.Scheduled);
    }

    // msiexec вернул 3010: пакет лёг, но занятые файлы Windows заменит только при перезагрузке.
    // Называть это «установлено» — врать платформе о версии, на которой работает парк.
    [Fact]
    public async Task InstallAsync_WhenWindowsNeedsARestart_DoesNotClaimTheNewVersionIsRunning()
    {
        var instruction = CreateInstruction(UpdateComponentNames.AgentService);
        var store = new RecordingUpdateInstallStateStore();
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rolled back"));
        var restart = new RecordingRestartScheduler(UpdateRestartResult.Scheduled("restart scheduled"));
        var installer = new SafeUpdateInstaller(
            store,
            new RecordingInstallExecutor(UpdateInstallResult.RestartRequired("reboot required")),
            rollback,
            restart,
            Options.Create(new AgentOptions { AgentVersion = "1.2.2" }),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")));

        var result = await installer.InstallAsync(instruction, CreateArtifact(instruction), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.RestartPending);
        Assert.Equal(UpdateStatusNames.PendingRestart, store.States[^1].Status);
        Assert.Equal("1.2.2", store.States[^1].InstalledVersion);
        // Откатывать нечего, перезапускать службу незачем, и запоминать пакет «хорошим» рано:
        // он ещё ни разу не запускался.
        Assert.Empty(rollback.RolledBackPlans);
        Assert.Empty(restart.ScheduledInstructions);
        Assert.Null(store.LastKnownGood);
    }

    // Кассира ради установки выгнали из приложения — вернуть его на экран нужно и в этом случае.
    [Fact]
    public async Task InstallAsync_WhenWindowsNeedsARestart_StillBringsTheClubApplicationBack()
    {
        var instruction = CreateInstruction(UpdateComponentNames.OrganizationAdmin);
        var launcher = new RecordingOrganizationAdminProcessLauncher();
        var installer = new SafeUpdateInstaller(
            new RecordingUpdateInstallStateStore(),
            new RecordingInstallExecutor(UpdateInstallResult.RestartRequired("reboot required")),
            new RecordingRollbackExecutor(UpdateRollbackResult.Success("rolled back")),
            new RecordingRestartScheduler(UpdateRestartResult.Scheduled("restart scheduled")),
            Options.Create(new AgentOptions()),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T17:00:00Z")),
            launcher);

        await installer.InstallAsync(instruction, CreateArtifact(instruction), CancellationToken.None);

        Assert.Equal(instruction, Assert.Single(launcher.Scheduled));
    }

    private static ComponentUpdateInstructionDto CreateInstruction(string component)
    {
        return new ComponentUpdateInstructionDto(
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Component: component,
            Version: "1.2.3",
            Channel: UpdateChannelNames.Beta,
            ArtifactUri: "https://updates.afk4.test/package.msi",
            Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Signature: "base64-signature",
            SignatureAlgorithm: "ed25519",
            SizeBytes: 123,
            ReleaseNotes: "update");
    }

    private static DownloadedUpdateArtifact CreateArtifact(ComponentUpdateInstructionDto instruction)
    {
        return new DownloadedUpdateArtifact(
            instruction,
            Path.Combine(Path.GetTempPath(), $"{instruction.UpdatePackageId:D}.msi"),
            instruction.SizeBytes);
    }

    private sealed class RecordingUpdateInstallStateStore : IUpdateInstallStateStore
    {
        public List<UpdateInstallState> States { get; } = [];

        public Task<UpdateInstallState> BeginInstallAsync(
            ComponentUpdateInstructionDto instruction,
            DownloadedUpdateArtifact artifact,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            var state = UpdateInstallState.Create(
                instruction,
                artifact,
                UpdateStatusNames.Installing,
                "installing",
                observedAtUtc);
            States.Add(state);

            return Task.FromResult(state);
        }

        public Task SaveAsync(UpdateInstallState state, CancellationToken cancellationToken)
        {
            States.Add(state);

            return Task.CompletedTask;
        }

        public LastKnownGoodUpdate? LastKnownGood { get; set; }

        public Task<LastKnownGoodUpdate?> LoadLastKnownGoodAsync(string component, CancellationToken cancellationToken)
        {
            return Task.FromResult(LastKnownGood);
        }

        public Task SaveLastKnownGoodAsync(LastKnownGoodUpdate lastKnownGood, CancellationToken cancellationToken)
        {
            LastKnownGood = lastKnownGood;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UpdateInstallState>> LoadRecoverableAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<UpdateInstallState>>([]);
        }
    }

    private sealed class RecordingInstallExecutor(UpdateInstallResult result) : IUpdateInstallExecutor
    {
        public List<UpdateInstallState> ExecutedPlans { get; } = [];

        public Task<UpdateInstallResult> ExecuteAsync(
            ComponentUpdateInstructionDto instruction,
            DownloadedUpdateArtifact artifact,
            UpdateInstallState state,
            CancellationToken cancellationToken)
        {
            ExecutedPlans.Add(state);

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingRollbackExecutor(UpdateRollbackResult result) : IUpdateRollbackExecutor
    {
        public List<UpdateInstallState> RolledBackPlans { get; } = [];

        public Task<UpdateRollbackResult> RollbackAsync(UpdateInstallState state, CancellationToken cancellationToken)
        {
            RolledBackPlans.Add(state);

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingRestartScheduler(UpdateRestartResult result) : IAgentRestartScheduler
    {
        public List<ComponentUpdateInstructionDto> ScheduledInstructions { get; } = [];

        public Task<UpdateRestartResult> ScheduleRestartAsync(
            ComponentUpdateInstructionDto instruction,
            UpdateInstallState state,
            CancellationToken cancellationToken)
        {
            if (instruction.Component == UpdateComponentNames.AgentService)
            {
                ScheduledInstructions.Add(instruction);
            }

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingOrganizationAdminProcessLauncher : IOrganizationAdminProcessLauncher
    {
        public List<ComponentUpdateInstructionDto> Scheduled { get; } = [];
        public Task ScheduleAfterRestartAsync(ComponentUpdateInstructionDto instruction, CancellationToken cancellationToken)
        {
            Scheduled.Add(instruction);
            return Task.CompletedTask;
        }
        public Task<bool> LaunchPendingAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
