using System.Security.Cryptography;
using System.Text;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
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

    private static SessionEnforcementCoordinator CreateCoordinator(
        ECDsa key,
        ISessionLeaseStore leaseStore,
        IAgentRuntimeStateStore runtimeStateStore,
        IWorkstationLockController lockController)
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
            new FixedTimeProvider(Now));
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

        public Task LockAsync(CancellationToken cancellationToken)
        {
            LockCount++;
            return Task.CompletedTask;
        }

        public Task UnlockAsync(CancellationToken cancellationToken)
        {
            UnlockCount++;
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"afk4-agent-enforcement-{Guid.NewGuid():N}"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
