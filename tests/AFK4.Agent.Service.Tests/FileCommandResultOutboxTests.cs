using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Tests;

public sealed class FileCommandResultOutboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void Constructor_WithoutOutboxFile_HasNoPending()
    {
        using var directory = TemporaryDirectory.Create();

        var outbox = new FileCommandResultOutbox(directory.Path);

        Assert.Empty(outbox.Pending);
    }

    [Fact]
    public void Enqueue_PersistsResultForRestart()
    {
        using var directory = TemporaryDirectory.Create();
        var result = CreateResult(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        var outbox = new FileCommandResultOutbox(directory.Path);

        outbox.Enqueue(result);
        var restarted = new FileCommandResultOutbox(directory.Path);

        Assert.Equal(new[] { result }, restarted.Pending);
    }

    [Fact]
    public void Enqueue_SameCommandTwice_ReplacesRatherThanDuplicates()
    {
        using var directory = TemporaryDirectory.Create();
        var commandId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var outbox = new FileCommandResultOutbox(directory.Path);

        outbox.Enqueue(CreateResult(commandId, status: "Pending"));
        outbox.Enqueue(CreateResult(commandId, status: "Completed"));

        var only = Assert.Single(outbox.Pending);
        Assert.Equal("Completed", only.Status);
    }

    [Fact]
    public void Acknowledge_RemovesDeliveredResult()
    {
        using var directory = TemporaryDirectory.Create();
        var commandId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var outbox = new FileCommandResultOutbox(directory.Path);
        outbox.Enqueue(CreateResult(commandId));

        outbox.Acknowledge(commandId);
        var restarted = new FileCommandResultOutbox(directory.Path);

        Assert.Empty(outbox.Pending);
        Assert.Empty(restarted.Pending);
    }

    [Fact]
    public void Acknowledge_UnknownCommand_IsNoOp()
    {
        using var directory = TemporaryDirectory.Create();
        var outbox = new FileCommandResultOutbox(directory.Path);
        outbox.Enqueue(CreateResult(Guid.Parse("11111111-1111-4111-8111-111111111111")));

        outbox.Acknowledge(Guid.Parse("22222222-2222-4222-8222-222222222222"));

        Assert.Single(outbox.Pending);
    }

    [Fact]
    public void Pending_PreservesEnqueueOrder()
    {
        using var directory = TemporaryDirectory.Create();
        var first = CreateResult(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        var second = CreateResult(Guid.Parse("22222222-2222-4222-8222-222222222222"));
        var outbox = new FileCommandResultOutbox(directory.Path);

        outbox.Enqueue(first);
        outbox.Enqueue(second);

        Assert.Equal(new[] { first, second }, outbox.Pending);
    }

    [Fact]
    public void Constructor_WithCorruptOutboxFile_HasNoPending()
    {
        using var directory = TemporaryDirectory.Create();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(
            Path.Combine(directory.Path, FileCommandResultOutbox.OutboxFileName),
            "not-json");

        var outbox = new FileCommandResultOutbox(directory.Path);

        Assert.Empty(outbox.Pending);
    }

    private static DeviceCommandResultDto CreateResult(Guid commandId, string status = "Completed")
    {
        return new DeviceCommandResultDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CommandId: commandId,
            Status: status,
            Message: "ok",
            ObservedAtUtc: Now);
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
                $"afk4-agent-cmd-outbox-{Guid.NewGuid():N}"));
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
