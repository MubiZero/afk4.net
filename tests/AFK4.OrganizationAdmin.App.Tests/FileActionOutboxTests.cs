using AFK4.OrganizationAdmin.App.FloorMap;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class FileActionOutboxTests
{
    private static readonly Guid DeviceA = Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111");
    private static readonly Guid DeviceB = Guid.Parse("bbbbbbbb-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset QueuedAt = DateTimeOffset.Parse("2026-06-02T10:00:00Z");

    [Fact]
    public void Pending_WhenNoFileExists_IsEmpty()
    {
        using var directory = new TemporaryDirectory();
        var outbox = new FileActionOutbox(directory.Path);

        Assert.Empty(outbox.Pending);
    }

    [Fact]
    public void Enqueue_PersistsAcrossInstances()
    {
        using var directory = new TemporaryDirectory();
        new FileActionOutbox(directory.Path).Enqueue(Entry(DeviceA, "lock"));

        var reopened = new FileActionOutbox(directory.Path);

        var entry = Assert.Single(reopened.Pending);
        Assert.Equal(DeviceA, entry.DeviceId);
        Assert.Equal("lock", entry.CommandType);
    }

    [Fact]
    public void Enqueue_SameDeviceReplacesPriorAction()
    {
        using var directory = new TemporaryDirectory();
        var outbox = new FileActionOutbox(directory.Path);
        outbox.Enqueue(Entry(DeviceA, "lock"));

        outbox.Enqueue(Entry(DeviceA, "unlock"));

        var entry = Assert.Single(outbox.Pending);
        Assert.Equal("unlock", entry.CommandType);
    }

    [Fact]
    public void Pending_PreservesEnqueueOrderAcrossDevices()
    {
        using var directory = new TemporaryDirectory();
        var outbox = new FileActionOutbox(directory.Path);
        outbox.Enqueue(Entry(DeviceA, "lock"));
        outbox.Enqueue(Entry(DeviceB, "unlock"));

        Assert.Equal([DeviceA, DeviceB], outbox.Pending.Select(entry => entry.DeviceId).ToArray());
    }

    [Fact]
    public void Acknowledge_RemovesEntryAndPersists()
    {
        using var directory = new TemporaryDirectory();
        var outbox = new FileActionOutbox(directory.Path);
        var entry = Entry(DeviceA, "lock");
        outbox.Enqueue(entry);

        outbox.Acknowledge(entry.IdempotencyKey);

        Assert.Empty(outbox.Pending);
        Assert.Empty(new FileActionOutbox(directory.Path).Pending);
    }

    [Fact]
    public void Acknowledge_UnknownKeyIsNoOp()
    {
        using var directory = new TemporaryDirectory();
        var outbox = new FileActionOutbox(directory.Path);
        outbox.Enqueue(Entry(DeviceA, "lock"));

        outbox.Acknowledge(Guid.NewGuid());

        Assert.Single(outbox.Pending);
    }

    [Fact]
    public void Pending_WhenFileIsCorrupt_IsEmptyAndSelfHeals()
    {
        using var directory = new TemporaryDirectory();
        new FileActionOutbox(directory.Path).Enqueue(Entry(DeviceA, "lock"));
        var file = Directory.GetFiles(directory.Path).Single();
        File.WriteAllText(file, "not json at all");

        Assert.Empty(new FileActionOutbox(directory.Path).Pending);
        Assert.False(File.Exists(file));
    }

    private static OperatorActionOutboxEntry Entry(Guid deviceId, string commandType)
    {
        return new OperatorActionOutboxEntry(
            IdempotencyKey: Guid.NewGuid(),
            DeviceId: deviceId,
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            SeatName: "PC-010",
            CommandType: commandType,
            ExpectedSessionId: null,
            QueuedAtUtc: QueuedAt);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"afk4-action-outbox-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
