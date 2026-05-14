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
    public async Task InstallAsync_WhenExecutorFailsRollsBackAndReturnsFailure()
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
        Assert.Equal(
            [UpdateStatusNames.Installing, UpdateStatusNames.Installing, UpdateStatusNames.RollbackStarted, UpdateStatusNames.RolledBack],
            store.States.Select(state => state.Status));
        Assert.Single(rollback.RolledBackPlans);
        Assert.Contains("install failed", result.Message, StringComparison.Ordinal);
        Assert.Contains("rollback complete", result.Message, StringComparison.Ordinal);
        Assert.Empty(restart.ScheduledInstructions);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
