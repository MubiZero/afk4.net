using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class SessionCommandHandlerLeaseTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly Guid SessionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task HandleAsync_UnlockWithValidLeaseStoresLeaseAndAcceptsCommand()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var lease = CreateSignedLease(key, sequence: 1);
        var store = new InMemorySessionLeaseStore();
        var handler = CreateHandler(key, store, out _, out _);

        var result = await handler.HandleAsync(CreateCommand("unlock", lease), CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(lease, store.Current);
    }

    [Fact]
    public async Task HandleAsync_UnlockWithBackendSerializedLeaseStoresLeaseAndAcceptsCommand()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var lease = CreateSignedLease(key, sequence: 1);
        var store = new InMemorySessionLeaseStore();
        var handler = CreateHandler(key, store, out _, out _);

        var result = await handler.HandleAsync(CreateCommand("unlock", lease, useBackendJson: true), CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(lease, store.Current);
    }

    [Fact]
    public async Task HandleAsync_UnlockWithoutLeaseRejectsCommand()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new InMemorySessionLeaseStore();
        var handler = CreateHandler(key, store, out _, out _);
        var command = new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: "unlock",
            CreatedAtUtc: Now,
            Payload: new Dictionary<string, string>
            {
                ["sessionId"] = SessionId.ToString("D")
            });

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains("sessionLease", result.Message, StringComparison.Ordinal);
        Assert.Null(store.Current);
    }

    [Fact]
    public async Task HandleAsync_RefreshSessionLeaseReplacesCurrentLease()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var oldLease = CreateSignedLease(key, sequence: 1);
        var newLease = CreateSignedLease(key, sequence: 2);
        var store = new InMemorySessionLeaseStore();
        store.Save(oldLease);
        var handler = CreateHandler(key, store, out _, out _);

        var result = await handler.HandleAsync(CreateCommand("refresh-session-lease", newLease), CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(newLease, store.Current);
    }

    [Fact]
    public async Task HandleAsync_LockClearsMatchingCurrentLease()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var lease = CreateSignedLease(key, sequence: 1);
        var store = new InMemorySessionLeaseStore();
        store.Save(lease);
        var handler = CreateHandler(key, store, out var runtimeStore, out var lockController);
        var command = new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: "lock",
            CreatedAtUtc: Now,
            Payload: new Dictionary<string, string>
            {
                ["sessionId"] = SessionId.ToString("D"),
                ["reason"] = "session-end"
            });

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("Accepted", result.Status);
        Assert.Null(store.Current);
        Assert.True(runtimeStore.Current.IsLocked);
        Assert.Equal(1, lockController.LockCount);
    }

    private static DefaultDeviceCommandHandler CreateHandler(
        ECDsa key,
        InMemorySessionLeaseStore store,
        out RecordingRuntimeStateStore runtimeStore,
        out RecordingWorkstationLockController lockController)
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
        runtimeStore = new RecordingRuntimeStateStore();
        lockController = new RecordingWorkstationLockController();
        var coordinator = new SessionEnforcementCoordinator(
            validator,
            store,
            runtimeStore,
            lockController,
            new FixedTimeProvider(Now));

        return new DefaultDeviceCommandHandler(options, coordinator);
    }

    private static DeviceCommandDto CreateCommand(string type, SessionLeaseDto lease, bool useBackendJson = false)
    {
        var leaseJson = useBackendJson
            ? JsonSerializer.Serialize(lease, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            : JsonSerializer.Serialize(lease);

        return new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: type,
            CreatedAtUtc: Now,
            Payload: new Dictionary<string, string>
            {
                ["sessionId"] = lease.SessionId.ToString("D"),
                ["sessionLease"] = leaseJson
            });
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
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
}
