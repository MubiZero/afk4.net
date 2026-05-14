using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Tests;

public sealed class FileSessionLeaseStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void Constructor_WithoutLeaseFile_HasNoCurrentLease()
    {
        using var directory = TemporaryDirectory.Create();

        var store = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Null(store.Current);
    }

    [Fact]
    public void Save_PersistsCurrentLeaseForRestart()
    {
        using var directory = TemporaryDirectory.Create();
        var lease = CreateLease(expiresAtUtc: Now.AddMinutes(15));
        var store = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));

        store.Save(lease);
        var restarted = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Equal(lease, restarted.Current);
    }

    [Fact]
    public void Clear_RemovesMatchingPersistedLease()
    {
        using var directory = TemporaryDirectory.Create();
        var lease = CreateLease(expiresAtUtc: Now.AddMinutes(15));
        var store = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        store.Save(lease);

        store.Clear(lease.SessionId);
        var restarted = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Null(restarted.Current);
        Assert.False(File.Exists(Path.Combine(directory.Path, FileSessionLeaseStore.LeaseFileName)));
    }

    [Fact]
    public void Constructor_WithExpiredLease_IgnoresLease()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));
        store.Save(CreateLease(expiresAtUtc: Now.AddMinutes(-1)));

        var restarted = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Null(restarted.Current);
    }

    [Fact]
    public void Constructor_WithCorruptLeaseFile_IgnoresLease()
    {
        using var directory = TemporaryDirectory.Create();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(
            Path.Combine(directory.Path, FileSessionLeaseStore.LeaseFileName),
            "not-json");

        var store = new FileSessionLeaseStore(directory.Path, new FixedTimeProvider(Now));

        Assert.Null(store.Current);
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
            IssuedAtUtc: Now,
            ExpiresAtUtc: expiresAtUtc,
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
                $"afk4-agent-lease-{Guid.NewGuid():N}"));
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
