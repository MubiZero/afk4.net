using System.Security.Cryptography;
using System.Text;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class SessionEnforcementCoordinatorTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly Guid SessionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public async Task UnlockAsync_WithValidLease_SavesLeaseMarksActiveAndUnlocks()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var lease = CreateSignedLease(key, sequence: 1);
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var lockController = new RecordingWorkstationLockController();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, lockController);

        var result = await coordinator.UnlockAsync(lease, CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(lease, leaseStore.Current);
        Assert.Equal(PlayerShellStateNames.Active, runtimeStore.Current.State);
        Assert.Equal(1, lockController.UnlockCount);
        Assert.Equal(0, lockController.LockCount);
    }

    [Fact]
    public async Task RefreshLeaseAsync_WithValidLease_ReplacesLeaseWithoutUnlockingAgain()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var oldLease = CreateSignedLease(key, sequence: 1);
        var newLease = CreateSignedLease(key, sequence: 2);
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var lockController = new RecordingWorkstationLockController();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, lockController);
        leaseStore.Save(oldLease);

        var result = await coordinator.RefreshLeaseAsync(newLease, CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(newLease, leaseStore.Current);
        Assert.Equal(newLease.SessionId, runtimeStore.Current.ActiveSessionId);
        Assert.Equal(0, lockController.UnlockCount);
        Assert.Equal(0, lockController.LockCount);
    }

    [Fact]
    public async Task LockAsync_ClearsLeaseMarksLockedAndLocksWorkstation()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var lease = CreateSignedLease(key, sequence: 1);
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var lockController = new RecordingWorkstationLockController();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, lockController);
        leaseStore.Save(lease);
        runtimeStore.MarkActive(lease, Now);

        var result = await coordinator.LockAsync(lease.SessionId, CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Null(leaseStore.Current);
        Assert.Equal(PlayerShellStateNames.Locked, runtimeStore.Current.State);
        Assert.Equal(1, lockController.LockCount);
    }

    // После сессии ПК убирают: игры закрываются, следы стираются, и оператор читает итог в ответе.
    [Fact]
    public async Task LockAsync_AfterASession_CleansUp_FromWhenThePcOpened()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var lease = CreateSignedLease(key, sequence: 1);
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var cleanup = new RecordingCleanup();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, new RecordingWorkstationLockController(), cleanup);
        leaseStore.Save(lease);
        runtimeStore.MarkActive(lease, Now.AddMinutes(-40));
        runtimeStore.MarkActive(lease, Now.AddMinutes(-10));

        var result = await coordinator.LockAsync(lease.SessionId, CancellationToken.None);

        // Продление аренды — не новая сессия: закрывается всё, что стартовало с первого открытия.
        Assert.Equal([Now.AddMinutes(-40)], cleanup.Runs);
        Assert.Contains("closed 2 app(s); cleared steam", result.Message, StringComparison.Ordinal);
    }

    // Свободный ПК запирают ещё раз — это не повод закрывать на нём что-то.
    [Fact]
    public async Task LockAsync_OnAnIdlePc_DoesNotCleanUp()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var cleanup = new RecordingCleanup();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, new RecordingWorkstationLockController(), cleanup);

        await coordinator.LockAsync(null, CancellationToken.None);

        Assert.Empty(cleanup.Runs);
    }

    private sealed class RecordingCleanup : AFK4.Agent.Service.Cleanup.ISessionCleanup
    {
        public List<DateTimeOffset?> Runs { get; } = [];

        public Task<AFK4.Agent.Service.Cleanup.SessionCleanupOutcome> RunAsync(DateTimeOffset? sessionStartedAtUtc, CancellationToken cancellationToken)
        {
            Runs.Add(sessionStartedAtUtc);
            return Task.FromResult(new AFK4.Agent.Service.Cleanup.SessionCleanupOutcome(2, ["steam"], []));
        }
    }

    [Fact]
    public async Task UnlockAsync_WithInvalidLease_RejectsAndDoesNotUnlock()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var lease = CreateSignedLease(key, sequence: 1) with { DeviceId = Guid.NewGuid() };
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var lockController = new RecordingWorkstationLockController();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, lockController);

        var result = await coordinator.UnlockAsync(lease, CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("device", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(leaseStore.Current);
        Assert.Equal(0, lockController.UnlockCount);
    }

    // «Заперто» и «заперто, но на самой машине ничего не применилось» — разные новости для
    // оператора. Раньше оба случая уезжали в журнал одинаковым успехом, и англоязычное
    // «Workstation locked (nothing)» было единственным следом того, что диспетчер задач у гостя
    // остался открыт.
    [Fact]
    public async Task LockAsync_WhenNothingCouldBeEnforced_SaysSoInsteadOfReportingAPlainSuccess()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, new PowerlessWorkstationLockController());

        var result = await coordinator.LockAsync(SessionId, CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(DeviceCommandOutcomeNames.MachinePoliciesUnavailable, result.Outcome);
    }

    /// <summary>
    /// «Запереть» посреди обслуживания не выводит из него: иначе агент забыл бы, что проводник
    /// техника открыл он, и тот остался бы открытым после возврата в зал.
    /// </summary>
    [Fact]
    public async Task LockAsync_DuringMaintenance_KeepsThePcUnderMaintenance()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        runtimeStore.Save(AgentRuntimeState.Maintenance(Now, desktopOpened: true));
        var lockController = new RecordingWorkstationLockController();
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, lockController);

        var result = await coordinator.LockAsync(sessionId: null, CancellationToken.None);

        Assert.Equal(DeviceCommandOutcomeNames.MaintenanceStarted, result.Outcome);
        Assert.Equal(PlayerShellStateNames.Maintenance, runtimeStore.Current.State);
        Assert.True(runtimeStore.Current.MaintenanceDesktopOpened);
        Assert.Equal(0, lockController.LockCount);
    }

    [Fact]
    public async Task LockAsync_WhenPoliciesApply_ReportsThatTheWorkstationIsLocked()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var directory = TemporaryDirectory.Create();
        var leaseStore = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        var runtimeStore = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        var coordinator = CreateCoordinator(key, leaseStore, runtimeStore, new RecordingWorkstationLockController());

        var result = await coordinator.LockAsync(SessionId, CancellationToken.None);

        Assert.Equal(DeviceCommandOutcomeNames.WorkstationLocked, result.Outcome);
    }

    private static SessionEnforcementCoordinator CreateCoordinator(
        ECDsa key,
        ISessionLeaseStore leaseStore,
        IAgentRuntimeStateStore runtimeStateStore,
        IWorkstationLockController lockController,
        AFK4.Agent.Service.Cleanup.ISessionCleanup? cleanup = null)
    {
        var options = Options.Create(new AgentOptions
        {
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            DeviceId = DeviceId,
            MachineName = "PC-001",
            LeaseSigningPublicKeyPem = key.ExportSubjectPublicKeyInfoPem()
        });
        var validator = new SessionLeaseValidator(options, new FixedTimeProvider(Now));

        return new SessionEnforcementCoordinator(
            validator,
            leaseStore,
            runtimeStateStore,
            lockController,
            new FixedTimeProvider(Now),
            cleanup);
    }

    private static SessionLeaseDto CreateSignedLease(ECDsa key, int sequence)
    {
        var unsigned = new SessionLeaseDto(
            SessionId,
            OrganizationId,
            BranchId,
            SeatId,
            DeviceId,
            SessionStateNames.Active,
            sequence,
            IssuedAtUtc: Now,
            ExpiresAtUtc: Now.AddMinutes(15),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: string.Empty);
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(unsigned);
        var signature = key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256);

        return unsigned with { Signature = Convert.ToBase64String(signature) };
    }

    private sealed class RecordingWorkstationLockController : IWorkstationLockController
    {
        public int LockCount { get; private set; }

        public int UnlockCount { get; private set; }

        public Task<WorkstationLockOutcome> LockAsync(CancellationToken cancellationToken)
        {
            LockCount++;
            return Task.FromResult(new WorkstationLockOutcome(["task manager disabled"]));
        }

        public Task<WorkstationLockOutcome> UnlockAsync(CancellationToken cancellationToken)
        {
            UnlockCount++;
            return Task.FromResult(new WorkstationLockOutcome(["task manager restored"]));
        }
    }

    /// <summary>Машина, на которой агент не смог применить ни одной политики.</summary>
    private sealed class PowerlessWorkstationLockController : IWorkstationLockController
    {
        public Task<WorkstationLockOutcome> LockAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WorkstationLockOutcome.Nothing);

        public Task<WorkstationLockOutcome> UnlockAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WorkstationLockOutcome.Nothing);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }

}
