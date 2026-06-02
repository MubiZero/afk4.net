using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Tests;

public sealed class GraceModeMonitorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public async Task EnforceAsync_WithCurrentLeaseBeforeExpiry_DoesNotLock()
    {
        var leaseStore = new InMemorySessionLeaseStore();
        var lease = CreateLease(Now.AddMinutes(5));
        leaseStore.Save(lease);
        var runtimeStore = new RecordingRuntimeStateStore();
        runtimeStore.MarkActive(lease, Now);
        var lockController = new RecordingWorkstationLockController();
        var monitor = new GraceModeMonitor(
            leaseStore,
            runtimeStore,
            lockController,
            new OfflineLeaseExtender(new OfflineGraceState()),
            new FixedTimeProvider(Now),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GraceModeMonitor>.Instance);

        await monitor.EnforceAsync(CancellationToken.None);

        Assert.Equal(lease, leaseStore.Current);
        Assert.Equal(PlayerShellStateNames.Active, runtimeStore.Current.State);
        Assert.Equal(0, lockController.LockCount);
    }

    [Fact]
    public async Task EnforceAsync_WithExpiredLease_ClearsLeaseAndLocksOnce()
    {
        var leaseStore = new InMemorySessionLeaseStore();
        var lease = CreateLease(Now.AddSeconds(-1));
        leaseStore.Save(lease);
        var runtimeStore = new RecordingRuntimeStateStore();
        runtimeStore.MarkActive(lease, Now.AddMinutes(-15));
        var lockController = new RecordingWorkstationLockController();
        var monitor = new GraceModeMonitor(
            leaseStore,
            runtimeStore,
            lockController,
            new OfflineLeaseExtender(new OfflineGraceState()),
            new FixedTimeProvider(Now),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GraceModeMonitor>.Instance);

        await monitor.EnforceAsync(CancellationToken.None);
        await monitor.EnforceAsync(CancellationToken.None);

        Assert.Null(leaseStore.Current);
        Assert.Equal(PlayerShellStateNames.Locked, runtimeStore.Current.State);
        Assert.Equal(1, lockController.LockCount);
    }

    [Fact]
    public async Task EnforceAsync_WithExpiredLeaseButWithinOfflineGrace_DoesNotLock()
    {
        var leaseStore = new InMemorySessionLeaseStore();
        var lease = CreateLease(Now.AddSeconds(-1));
        leaseStore.Save(lease);
        var runtimeStore = new RecordingRuntimeStateStore();
        runtimeStore.MarkActive(lease, Now.AddMinutes(-15));
        var lockController = new RecordingWorkstationLockController();
        var grace = new OfflineGraceState();
        grace.RecordSuccessfulContact(Now.AddMinutes(-3), effectiveGraceMinutes: 15); // dropped 3 min ago
        var monitor = new GraceModeMonitor(
            leaseStore,
            runtimeStore,
            lockController,
            new OfflineLeaseExtender(grace),
            new FixedTimeProvider(Now),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GraceModeMonitor>.Instance);

        await monitor.EnforceAsync(CancellationToken.None);

        Assert.Equal(lease, leaseStore.Current); // lease retained for reconnect
        Assert.Equal(0, lockController.LockCount);
    }

    private static SessionLeaseDto CreateLease(DateTimeOffset expiresAtUtc)
    {
        return new SessionLeaseDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: Now.AddMinutes(-15),
            ExpiresAtUtc: expiresAtUtc,
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
    }

    private sealed class RecordingRuntimeStateStore : IAgentRuntimeStateStore
    {
        public AgentRuntimeState Current { get; private set; } = AgentRuntimeState.Locked(Now);

        public void Save(AgentRuntimeState state)
        {
            Current = state;
        }

        public void MarkLocked(DateTimeOffset observedAtUtc)
        {
            Current = AgentRuntimeState.Locked(observedAtUtc);
        }

        public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc)
        {
            Current = AgentRuntimeState.Active(lease, observedAtUtc);
        }
    }

    private sealed class RecordingWorkstationLockController : IWorkstationLockController
    {
        public int LockCount { get; private set; }

        public Task LockAsync(CancellationToken cancellationToken)
        {
            LockCount++;
            return Task.CompletedTask;
        }

        public Task UnlockAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
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
