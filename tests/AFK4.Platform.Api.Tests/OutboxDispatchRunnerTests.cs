using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Tests.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class OutboxDispatchRunnerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T10:00:00Z");

    [Fact]
    public async Task RunAsync_DueRow_DispatchesAndMarksDispatched()
    {
        await using var db = CreateDbContext();
        await SeedRowAsync(db, "session.checkout", availableAt: Now);
        var handler = new FakeHandler("session.checkout", _ => OutboxHandlerResult.Ok());
        var runner = CreateRunner(db, new FixedTimeProvider(Now), handler);

        var processed = await runner.RunAsync(50, CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(1, handler.CallCount);
        var row = await db.OutboxMessages.SingleAsync();
        Assert.Equal(OutboxMessageStatus.Dispatched, row.Status);
        Assert.Equal(Now, row.DispatchedAtUtc);
        Assert.Equal(1, row.AttemptCount);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task RunAsync_RowNotYetDue_IsNotClaimed()
    {
        await using var db = CreateDbContext();
        await SeedRowAsync(db, "session.checkout", availableAt: Now.AddMinutes(5));
        var handler = new FakeHandler("session.checkout", _ => OutboxHandlerResult.Ok());
        var runner = CreateRunner(db, new FixedTimeProvider(Now), handler);

        var processed = await runner.RunAsync(50, CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(OutboxMessageStatus.Pending, (await db.OutboxMessages.SingleAsync()).Status);
    }

    [Fact]
    public async Task DispatchAsync_NoHandlerForType_MarksFailed()
    {
        await using var db = CreateDbContext();
        var runner = CreateRunner(db, new FixedTimeProvider(Now));
        var row = NewRow("unknown.type", Now);

        await runner.DispatchAsync(row, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Failed, row.Status);
        Assert.NotNull(row.LastError);
    }

    [Fact]
    public async Task DispatchAsync_TransientFailure_StaysPendingWithBackoff()
    {
        await using var db = CreateDbContext();
        var handler = new FakeHandler("t", _ => OutboxHandlerResult.TransientFailure("smtp down"));
        var runner = CreateRunner(db, new FixedTimeProvider(Now), handler);
        var row = NewRow("t", Now);

        await runner.DispatchAsync(row, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Pending, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.Equal(Now.AddMinutes(1), row.AvailableAtUtc); // first backoff step
        Assert.Equal("smtp down", row.LastError);
        Assert.Null(row.DispatchedAtUtc);
    }

    [Fact]
    public async Task DispatchAsync_TransientFailureAtMaxAttempts_MarksFailed()
    {
        await using var db = CreateDbContext();
        var handler = new FakeHandler("t", _ => OutboxHandlerResult.TransientFailure("still down"));
        var runner = CreateRunner(db, new FixedTimeProvider(Now), handler);
        var row = NewRow("t", Now);
        row.AttemptCount = 2; // MaxAttempts = 3, so this attempt is the last

        await runner.DispatchAsync(row, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Failed, row.Status);
        Assert.Equal(3, row.AttemptCount);
    }

    [Fact]
    public async Task DispatchAsync_PermanentFailure_MarksFailedImmediately()
    {
        await using var db = CreateDbContext();
        var handler = new FakeHandler("t", _ => OutboxHandlerResult.PermanentFailure("bad payload"));
        var runner = CreateRunner(db, new FixedTimeProvider(Now), handler);
        var row = NewRow("t", Now);

        await runner.DispatchAsync(row, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Failed, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.Equal("bad payload", row.LastError);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_TreatedAsTransient()
    {
        await using var db = CreateDbContext();
        var handler = new FakeHandler("t", _ => throw new InvalidOperationException("boom"));
        var runner = CreateRunner(db, new FixedTimeProvider(Now), handler);
        var row = NewRow("t", Now);

        await runner.DispatchAsync(row, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Pending, row.Status);
        Assert.Equal("boom", row.LastError);
    }

    private static OutboxMessageEntity NewRow(string type, DateTimeOffset availableAt) =>
        new()
        {
            OutboxMessageId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Type = type,
            PayloadJson = "{}",
            Status = OutboxMessageStatus.Pending,
            AvailableAtUtc = availableAt,
            CreatedAtUtc = availableAt,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

    private static async Task SeedRowAsync(PlatformDbContext db, string type, DateTimeOffset availableAt)
    {
        db.OutboxMessages.Add(NewRow(type, availableAt));
        await db.SaveChangesAsync();
    }

    private static OutboxDispatchRunner CreateRunner(
        PlatformDbContext db,
        TimeProvider timeProvider,
        params IOutboxMessageHandler[] handlers)
    {
        var options = Options.Create(new OutboxOptions
        {
            MaxAttempts = 3,
            BackoffSchedule = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]
        });
        return new OutboxDispatchRunner(new EfBillingOutbox(db), handlers, timeProvider, options);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private sealed class FakeHandler(string type, Func<OutboxMessageEntity, OutboxHandlerResult> handle) : IOutboxMessageHandler
    {
        public string Type => type;

        public int CallCount { get; private set; }

        public Task<OutboxHandlerResult> HandleAsync(OutboxMessageEntity message, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(handle(message));
        }
    }
}
