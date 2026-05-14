using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Tests;

public sealed class AgentRuntimeStateStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void Constructor_WithoutStateFile_StartsLocked()
    {
        using var directory = TemporaryDirectory.Create();

        var store = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Equal(PlayerShellStateNames.Locked, store.Current.State);
        Assert.True(store.Current.IsLocked);
        Assert.Null(store.Current.ActiveSessionId);
    }

    [Fact]
    public void MarkActive_PersistsRuntimeStateForRestart()
    {
        using var directory = TemporaryDirectory.Create();
        var lease = CreateLease();
        var store = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));

        store.MarkActive(lease, Now);
        var restarted = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Equal(PlayerShellStateNames.Active, restarted.Current.State);
        Assert.False(restarted.Current.IsLocked);
        Assert.Equal(lease.SessionId, restarted.Current.ActiveSessionId);
        Assert.Equal(lease.ExpiresAtUtc, restarted.Current.LeaseExpiresAtUtc);
    }

    [Fact]
    public void MarkLocked_PersistsLockedRuntimeState()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));
        store.MarkActive(CreateLease(), Now);

        store.MarkLocked(Now.AddMinutes(1));
        var restarted = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now.AddMinutes(1)));

        Assert.Equal(PlayerShellStateNames.Locked, restarted.Current.State);
        Assert.True(restarted.Current.IsLocked);
        Assert.Null(restarted.Current.ActiveSessionId);
    }

    [Fact]
    public void Constructor_WithCorruptStateFile_StartsLocked()
    {
        using var directory = TemporaryDirectory.Create();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(
            Path.Combine(directory.Path, AgentRuntimeStateStore.StateFileName),
            "not-json");

        var store = new AgentRuntimeStateStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Equal(PlayerShellStateNames.Locked, store.Current.State);
        Assert.True(store.Current.IsLocked);
    }

    private static SessionLeaseDto CreateLease()
    {
        return new SessionLeaseDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: Now,
            ExpiresAtUtc: Now.AddMinutes(15),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
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
                $"afk4-agent-runtime-{Guid.NewGuid():N}"));
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
