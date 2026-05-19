using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Tests;

public sealed class UpdateRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAsync_RollsBackRecoverableInstallAndReportsBackendStatuses()
    {
        var state = CreateState(UpdateStatusNames.Installing);
        var store = new RecordingUpdateInstallStateStore([state]);
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var updateClient = new RecordingAgentUpdateClient();
        var recovery = new UpdateRecoveryService(
            store,
            rollback,
            updateClient,
            new FixedComponentVersionProvider([new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T18:00:00Z")));

        await recovery.RecoverAsync(CancellationToken.None);

        Assert.Single(rollback.RolledBackStates);
        Assert.Equal(
            [UpdateStatusNames.RollbackStarted, UpdateStatusNames.RolledBack],
            updateClient.ReportedStatuses.Select(report => report.Status));
        Assert.Equal(
            [UpdateStatusNames.RollbackStarted, UpdateStatusNames.RolledBack],
            store.SavedStates.Select(saved => saved.Status));
    }

    [Fact]
    public async Task RecoverAsync_WhenRollbackFailsReportsFailed()
    {
        var state = CreateState(UpdateStatusNames.RollbackStarted);
        var store = new RecordingUpdateInstallStateStore([state]);
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Failed("rollback failed"));
        var updateClient = new RecordingAgentUpdateClient();
        var recovery = new UpdateRecoveryService(
            store,
            rollback,
            updateClient,
            new FixedComponentVersionProvider([new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")]),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T18:00:00Z")));

        await recovery.RecoverAsync(CancellationToken.None);

        Assert.Equal(
            [UpdateStatusNames.RollbackStarted, UpdateStatusNames.Failed],
            updateClient.ReportedStatuses.Select(report => report.Status));
        Assert.Equal(UpdateStatusNames.Failed, store.SavedStates[^1].Status);
    }

    [Fact]
    public async Task RecoverAsync_WhenTargetVersionIsInstalledReportsInstalledWithoutRollback()
    {
        var state = CreateState(UpdateStatusNames.Installing);
        var store = new RecordingUpdateInstallStateStore([state]);
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var updateClient = new RecordingAgentUpdateClient();
        var recovery = new UpdateRecoveryService(
            store,
            rollback,
            updateClient,
            new FixedComponentVersionProvider([new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.3")]),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T18:00:00Z")));

        await recovery.RecoverAsync(CancellationToken.None);

        Assert.Empty(rollback.RolledBackStates);
        var report = Assert.Single(updateClient.ReportedStatuses);
        Assert.Equal(UpdateStatusNames.Installed, report.Status);
        Assert.Equal("Interrupted update completed before Agent restart.", report.Message);
        Assert.Equal(UpdateStatusNames.Installed, store.SavedStates.Single().Status);
        Assert.Equal("1.2.3", store.SavedStates.Single().InstalledVersion);
    }

    [Fact]
    public async Task RecoverAsync_WhenInstalledMsiVersionMatchesPrereleaseTargetReportsInstalledWithoutRollback()
    {
        var state = CreateState(UpdateStatusNames.Installing) with
        {
            TargetVersion = "1.2.3-ci-minio-rollout"
        };
        var store = new RecordingUpdateInstallStateStore([state]);
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var updateClient = new RecordingAgentUpdateClient();
        var recovery = new UpdateRecoveryService(
            store,
            rollback,
            updateClient,
            new FixedComponentVersionProvider([new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.3")]),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T18:00:00Z")));

        await recovery.RecoverAsync(CancellationToken.None);

        Assert.Empty(rollback.RolledBackStates);
        var report = Assert.Single(updateClient.ReportedStatuses);
        Assert.Equal(UpdateStatusNames.Installed, report.Status);
        Assert.Equal(UpdateStatusNames.Installed, store.SavedStates.Single().Status);
        Assert.Equal("1.2.3", store.SavedStates.Single().InstalledVersion);
    }

    [Fact]
    public async Task RecoverAsync_WhenInstalledVersionIsNewerThanRecoverableTargetReportsSupersededWithoutRollback()
    {
        var state = CreateState(UpdateStatusNames.Installing) with
        {
            TargetVersion = "1.2.3"
        };
        var store = new RecordingUpdateInstallStateStore([state]);
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var updateClient = new RecordingAgentUpdateClient();
        var recovery = new UpdateRecoveryService(
            store,
            rollback,
            updateClient,
            new FixedComponentVersionProvider([new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.4")]),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T18:00:00Z")));

        await recovery.RecoverAsync(CancellationToken.None);

        Assert.Empty(rollback.RolledBackStates);
        var report = Assert.Single(updateClient.ReportedStatuses);
        Assert.Equal(UpdateStatusNames.Superseded, report.Status);
        Assert.Equal("Recoverable update was superseded by a newer installed version.", report.Message);
        Assert.Equal(UpdateStatusNames.Superseded, store.SavedStates.Single().Status);
        Assert.Equal("1.2.4", store.SavedStates.Single().InstalledVersion);
    }

    [Fact]
    public async Task RecoverAsync_WhenInstalledVersionIsNewerThanPrereleaseTargetReportsSupersededWithoutRollback()
    {
        var state = CreateState(UpdateStatusNames.Installing) with
        {
            TargetVersion = "1.2.3-ci-minio-rollout"
        };
        var store = new RecordingUpdateInstallStateStore([state]);
        var rollback = new RecordingRollbackExecutor(UpdateRollbackResult.Success("rollback complete"));
        var updateClient = new RecordingAgentUpdateClient();
        var recovery = new UpdateRecoveryService(
            store,
            rollback,
            updateClient,
            new FixedComponentVersionProvider([new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.4")]),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T18:00:00Z")));

        await recovery.RecoverAsync(CancellationToken.None);

        Assert.Empty(rollback.RolledBackStates);
        Assert.Equal(UpdateStatusNames.Superseded, store.SavedStates.Single().Status);
        Assert.Equal(UpdateStatusNames.Superseded, updateClient.ReportedStatuses.Single().Status);
    }

    private static UpdateInstallState CreateState(string status)
    {
        return new UpdateInstallState(
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Component: UpdateComponentNames.AgentService,
            InstalledVersion: "1.2.2",
            TargetVersion: "1.2.3",
            ArtifactPath: Path.Combine(Path.GetTempPath(), "agent.msi"),
            Status: status,
            Message: "recoverable",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T17:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-14T17:00:00Z"));
    }

    private sealed class RecordingUpdateInstallStateStore(IReadOnlyList<UpdateInstallState> recoverableStates) : IUpdateInstallStateStore
    {
        public List<UpdateInstallState> SavedStates { get; } = [];

        public Task<UpdateInstallState> BeginInstallAsync(
            ComponentUpdateInstructionDto instruction,
            DownloadedUpdateArtifact artifact,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync(UpdateInstallState state, CancellationToken cancellationToken)
        {
            SavedStates.Add(state);

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UpdateInstallState>> LoadRecoverableAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(recoverableStates);
        }
    }

    private sealed class RecordingRollbackExecutor(UpdateRollbackResult result) : IUpdateRollbackExecutor
    {
        public List<UpdateInstallState> RolledBackStates { get; } = [];

        public Task<UpdateRollbackResult> RollbackAsync(UpdateInstallState state, CancellationToken cancellationToken)
        {
            RolledBackStates.Add(state);

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingAgentUpdateClient : IAgentUpdateClient
    {
        public List<ReportedStatus> ReportedStatuses { get; } = [];

        public Task<UpdateCheckResult> CheckForUpdatesAsync(
            IReadOnlyList<DeviceComponentVersionDto> installedComponents,
            DateTimeOffset checkedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DeviceUpdateStatusResultDto> ReportStatusAsync(
            Guid updateRolloutId,
            Guid updatePackageId,
            string component,
            string installedVersion,
            string targetVersion,
            string status,
            string message,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            ReportedStatuses.Add(new ReportedStatus(status, message));

            return Task.FromResult(new DeviceUpdateStatusResultDto(
                Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
                updateRolloutId,
                updatePackageId,
                component,
                status,
                message,
                observedAtUtc));
        }
    }

    private sealed record ReportedStatus(string Status, string Message);

    private sealed class FixedComponentVersionProvider(IReadOnlyList<DeviceComponentVersionDto> components) : IAgentComponentVersionProvider
    {
        public IReadOnlyList<DeviceComponentVersionDto> GetInstalledComponents()
        {
            return components;
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
