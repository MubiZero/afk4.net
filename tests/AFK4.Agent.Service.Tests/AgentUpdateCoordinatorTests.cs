using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class AgentUpdateCoordinatorTests
{
    [Fact]
    public async Task CheckAndApplyUpdatesAsync_DownloadsVerifiesInstallsAndReportsStatusProgress()
    {
        var instruction = CreateInstruction();
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var downloader = new RecordingUpdateArtifactDownloader();
        var verifier = new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Valid("hash verified"));
        var installer = new RecordingUpdateInstaller(UpdateInstallResult.Success("installer completed"));
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions())),
            downloader,
            verifier,
            installer,
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            FreeSeatGuard());

        var result = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal(1, result.OfferedCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal([instruction], downloader.DownloadedInstructions);
        Assert.Equal([instruction], installer.InstalledInstructions);
        Assert.Equal(
            [
                UpdateStatusNames.Offered,
                UpdateStatusNames.Downloading,
                UpdateStatusNames.Downloaded,
                UpdateStatusNames.Installing,
                UpdateStatusNames.Installed
            ],
            updateClient.ReportedStatuses.Select(report => report.Status));
        Assert.All(updateClient.ReportedStatuses.Take(4), report => Assert.Equal("1.2.2", report.InstalledVersion));
        Assert.Equal("1.2.3", updateClient.ReportedStatuses[^1].InstalledVersion);
        Assert.All(updateClient.ReportedStatuses, report => Assert.Equal("1.2.3", report.TargetVersion));
    }

    [Fact]
    public async Task CheckAndApplyUpdatesAsync_WhenVerificationFailsReportsFailedAndDoesNotInstall()
    {
        var instruction = CreateInstruction();
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var installer = new RecordingUpdateInstaller(UpdateInstallResult.Success("installer completed"));
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions())),
            new RecordingUpdateArtifactDownloader(),
            new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Invalid("sha mismatch")),
            installer,
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            FreeSeatGuard());

        var result = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal(1, result.OfferedCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Empty(installer.InstalledInstructions);
        var failed = Assert.Single(updateClient.ReportedStatuses, report => report.Status == UpdateStatusNames.Failed);
        Assert.Equal("sha mismatch", failed.Message);
    }

    [Fact]
    public async Task CheckAndApplyUpdatesAsync_WhenInstallFailsReportsFailed()
    {
        var instruction = CreateInstruction();
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions())),
            new RecordingUpdateArtifactDownloader(),
            new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Valid("hash verified")),
            new RecordingUpdateInstaller(UpdateInstallResult.Failed("installer exit code 1")),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            FreeSeatGuard());

        var result = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal(1, result.OfferedCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.FailedCount);
        var failed = Assert.Single(updateClient.ReportedStatuses, report => report.Status == UpdateStatusNames.Failed);
        Assert.Equal("installer exit code 1", failed.Message);
    }

    [Fact]
    public async Task CheckAndApplyUpdatesAsync_WhenAdminUpdateIsDeferred_RetriesOnNextPollWithoutDownload()
    {
        var instruction = CreateInstruction() with { Component = UpdateComponentNames.OrganizationAdmin };
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var downloader = new RecordingUpdateArtifactDownloader();
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions(DeviceRoleNames.ManagerWorkstation, organizationAdminVersion: "1.2.2"))),
            downloader,
            new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Valid("verified")),
            new RecordingUpdateInstaller(UpdateInstallResult.Success("installed")),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            FreeSeatGuard(),
            new FixedOrganizationAdminReadiness(new(
                OrganizationAdminUpdateReadinessNames.DeferredOutsideWindow, "outside window")));

        var first = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);
        var second = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal(1, first.OfferedCount);
        Assert.Equal(1, second.OfferedCount);
        Assert.Empty(downloader.DownloadedInstructions);
        Assert.Equal(2, updateClient.ReportedStatuses.Count(status => status.Status == UpdateStatusNames.Deferred));
    }

    [Theory]
    [InlineData(UpdateComponentNames.AgentService)]
    [InlineData(UpdateComponentNames.PlayerShell)]
    public async Task CheckAndApplyUpdatesAsync_WhenGuestIsPlaying_DefersInsteadOfInterruptingTheSession(string component)
    {
        var instruction = CreateInstruction() with { Component = component };
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var downloader = new RecordingUpdateArtifactDownloader();
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions())),
            downloader,
            new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Valid("verified")),
            new RecordingUpdateInstaller(UpdateInstallResult.Success("installed")),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            BusySeatGuard());

        var first = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);
        var second = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal(0, first.AppliedCount);
        Assert.Equal(0, first.FailedCount);
        Assert.Empty(downloader.DownloadedInstructions);
        var deferred = updateClient.ReportedStatuses.Where(status => status.Status == UpdateStatusNames.Deferred).ToList();
        Assert.Equal(2, deferred.Count);
        Assert.Contains(BusySessionId.ToString("D"), deferred[0].Message);
        Assert.Equal(1, second.OfferedCount);
    }

    [Fact]
    public async Task CheckAndApplyUpdatesAsync_WhenSeatIsFreeAgain_InstallsThePreviouslyDeferredUpdate()
    {
        var instruction = CreateInstruction();
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var installer = new RecordingUpdateInstaller(UpdateInstallResult.Success("installed"));
        var runtimeStateStore = new SeatRuntimeStateStore(isLocked: false);
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions())),
            new RecordingUpdateArtifactDownloader(),
            new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Valid("verified")),
            installer,
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            new GuestSeatUpdateGuard(runtimeStateStore));

        await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);
        runtimeStateStore.MarkLocked(DateTimeOffset.Parse("2026-05-14T17:00:00Z"));
        await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal([instruction], installer.InstalledInstructions);
    }

    [Fact]
    public void GuestSeatUpdateGuard_DoesNotHoldBackTheClubApplicationUpdate()
    {
        var guard = new GuestSeatUpdateGuard(new SeatRuntimeStateStore(isLocked: false));

        Assert.True(guard.Evaluate(UpdateComponentNames.OrganizationAdmin).CanInstall);
        Assert.False(guard.Evaluate(UpdateComponentNames.PlayerShell).CanInstall);
    }

    [Fact]
    public async Task CheckAndApplyUpdatesAsync_WhenWindowsNeedsARestart_ReportsItInsteadOfInstalled()
    {
        var instruction = CreateInstruction();
        var updateClient = new RecordingAgentUpdateClient([instruction]);
        var coordinator = new AgentUpdateCoordinator(
            NullLogger<AgentUpdateCoordinator>.Instance,
            updateClient,
            new AgentComponentVersionProvider(Options.Create(CreateOptions())),
            new RecordingUpdateArtifactDownloader(),
            new FixedUpdatePackageVerifier(UpdatePackageVerificationResult.Valid("verified")),
            new RecordingUpdateInstaller(UpdateInstallResult.RestartRequired("reboot required")),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T16:00:00Z")),
            new InMemoryUpdateAttemptLedger(),
            FreeSeatGuard());

        var result = await coordinator.CheckAndApplyUpdatesAsync(CancellationToken.None);

        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.PendingRestartCount);
        var last = updateClient.ReportedStatuses[^1];
        Assert.Equal(UpdateStatusNames.PendingRestart, last.Status);
        // Устройство всё ещё на старой сборке — именно её и сообщаем.
        Assert.Equal("1.2.2", last.InstalledVersion);
    }

    [Fact]
    public void AgentComponentVersionProvider_ReturnsAgentAndPlayerShellVersionsForGamingPcRole()
    {
        var provider = new AgentComponentVersionProvider(Options.Create(
            CreateOptions(
                DeviceRoleNames.GamingPc,
                shellVersion: "1.2.1",
                organizationAdminVersion: "9.9.9")));

        var components = provider.GetInstalledComponents();

        Assert.Contains(components, component => component.Component == UpdateComponentNames.AgentService && component.Version == "1.2.2");
        Assert.Contains(components, component => component.Component == UpdateComponentNames.PlayerShell && component.Version == "1.2.1");
        Assert.DoesNotContain(components, component => component.Component == UpdateComponentNames.OrganizationAdmin);
    }

    [Fact]
    public void AgentComponentVersionProvider_ReturnsAgentAndOrganizationAdminVersionsForManagerWorkstationRole()
    {
        var provider = new AgentComponentVersionProvider(Options.Create(
            CreateOptions(
                DeviceRoleNames.ManagerWorkstation,
                shellVersion: "9.9.9",
                organizationAdminVersion: "1.2.4")));

        var components = provider.GetInstalledComponents();

        Assert.Contains(components, component => component.Component == UpdateComponentNames.AgentService && component.Version == "1.2.2");
        Assert.Contains(components, component => component.Component == UpdateComponentNames.OrganizationAdmin && component.Version == "1.2.4");
        Assert.DoesNotContain(components, component => component.Component == UpdateComponentNames.PlayerShell);
    }

    private static readonly Guid BusySessionId = Guid.Parse("5f2f6f4c-6a52-4a1c-9b1f-2a4a1f0f7c31");

    private static GuestSeatUpdateGuard FreeSeatGuard() => new(new SeatRuntimeStateStore(isLocked: true));

    private static GuestSeatUpdateGuard BusySeatGuard() => new(new SeatRuntimeStateStore(isLocked: false));

    private sealed class SeatRuntimeStateStore : IAgentRuntimeStateStore
    {
        public SeatRuntimeStateStore(bool isLocked)
        {
            Current = isLocked
                ? AgentRuntimeState.Locked(DateTimeOffset.Parse("2026-05-14T15:00:00Z"))
                : new AgentRuntimeState(
                    PlayerShellStateNames.Active,
                    IsLocked: false,
                    BusySessionId,
                    DateTimeOffset.Parse("2026-05-14T18:00:00Z"),
                    DateTimeOffset.Parse("2026-05-14T15:00:00Z"));
        }

        public AgentRuntimeState Current { get; private set; }

        public void Save(AgentRuntimeState state) => Current = state;

        public void MarkLocked(DateTimeOffset observedAtUtc) => Current = AgentRuntimeState.Locked(observedAtUtc);

        public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc) =>
            Current = AgentRuntimeState.Active(lease, observedAtUtc);
    }

    private static AgentOptions CreateOptions(
        string deviceRole = DeviceRoleNames.GamingPc,
        string shellVersion = "1.2.1",
        string organizationAdminVersion = "")
    {
        return new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            AgentVersion = "1.2.2",
            ShellVersion = shellVersion,
            DeviceRole = deviceRole,
            OrganizationAdminVersion = organizationAdminVersion
        };
    }

    private static ComponentUpdateInstructionDto CreateInstruction()
    {
        return new ComponentUpdateInstructionDto(
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Component: UpdateComponentNames.AgentService,
            Version: "1.2.3",
            Channel: UpdateChannelNames.Beta,
            ArtifactUri: "https://updates.afk4.test/agent/1.2.3/agent.msi",
            Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Signature: "base64-signature",
            SignatureAlgorithm: "ed25519",
            SizeBytes: 42_000_000,
            ReleaseNotes: "Agent update.");
    }

    private sealed class RecordingAgentUpdateClient(IReadOnlyList<ComponentUpdateInstructionDto> updates) : IAgentUpdateClient
    {
        public List<ReportedStatus> ReportedStatuses { get; } = [];

        public Task<UpdateCheckResult> CheckForUpdatesAsync(
            IReadOnlyList<DeviceComponentVersionDto> installedComponents,
            DateTimeOffset checkedAtUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new UpdateCheckResult(checkedAtUtc, updates));
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
            ReportedStatuses.Add(new ReportedStatus(
                status,
                message,
                installedVersion,
                targetVersion));

            return Task.FromResult(new DeviceUpdateStatusResultDto(
                CreateOptions().DeviceId,
                updateRolloutId,
                updatePackageId,
                component,
                status,
                message,
                observedAtUtc));
        }
    }

    private sealed record ReportedStatus(
        string Status,
        string Message,
        string InstalledVersion,
        string TargetVersion);

    private sealed class FixedOrganizationAdminReadiness(OrganizationAdminUpdateReadinessResult result)
        : IOrganizationAdminUpdateReadiness
    {
        public Task<OrganizationAdminUpdateReadinessResult> EvaluateAsync(
            ComponentUpdateInstructionDto instruction,
            OrganizationAdminUpdatePreferenceDto? preference,
            DateTimeOffset serverTimeUtc,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class RecordingUpdateArtifactDownloader : IUpdateArtifactDownloader
    {
        public List<ComponentUpdateInstructionDto> DownloadedInstructions { get; } = [];

        public Task<DownloadedUpdateArtifact> DownloadAsync(
            ComponentUpdateInstructionDto instruction,
            CancellationToken cancellationToken)
        {
            DownloadedInstructions.Add(instruction);

            return Task.FromResult(new DownloadedUpdateArtifact(
                instruction,
                Path.Combine(Path.GetTempPath(), $"{instruction.UpdatePackageId:D}.msi"),
                instruction.SizeBytes));
        }
    }

    private sealed class FixedUpdatePackageVerifier(UpdatePackageVerificationResult result) : IUpdatePackageVerifier
    {
        public Task<UpdatePackageVerificationResult> VerifyAsync(
            ComponentUpdateInstructionDto instruction,
            DownloadedUpdateArtifact artifact,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingUpdateInstaller(UpdateInstallResult result) : IUpdateInstaller
    {
        public List<ComponentUpdateInstructionDto> InstalledInstructions { get; } = [];

        public Task<UpdateInstallResult> InstallAsync(
            ComponentUpdateInstructionDto instruction,
            DownloadedUpdateArtifact artifact,
            CancellationToken cancellationToken)
        {
            InstalledInstructions.Add(instruction);

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

    /// <summary>Журнал попыток в памяти: тесты проверяют поведение координатора, а не файл.</summary>
    private sealed class InMemoryUpdateAttemptLedger : IUpdateAttemptLedger
    {
        private readonly Dictionary<Guid, int> attempts = [];

        public bool ShouldAttempt(Guid rolloutId) => attempts.GetValueOrDefault(rolloutId) < FileUpdateAttemptLedger.MaxAttempts;

        public void RecordAttempt(Guid rolloutId) => attempts[rolloutId] = attempts.GetValueOrDefault(rolloutId) + 1;

        public void ForgetAttempt(Guid rolloutId)
        {
            var current = attempts.GetValueOrDefault(rolloutId);
            if (current <= 1)
            {
                attempts.Remove(rolloutId);
            }
            else
            {
                attempts[rolloutId] = current - 1;
            }
        }
    }
}
