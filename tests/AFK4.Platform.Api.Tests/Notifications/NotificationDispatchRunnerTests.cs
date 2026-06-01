using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class NotificationDispatchRunnerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static NotificationOptions DefaultOptions() => new()
    {
        MaxAttempts = 3,
        BackoffSchedule = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)],
    };

    private static NotificationDispatchRunner CreateRunner(
        PlatformDbContext db,
        INotificationChannel channel,
        NotificationOptions? options = null) =>
        new(new EfNotificationOutbox(db), [channel], new FixedTimeProvider(Now), Options.Create(options ?? DefaultOptions()));

    private static async Task<NotificationOutboxEntity> SeedAsync(
        PlatformDbContext db,
        string channel = "Email",
        int attemptCount = 0,
        DateTimeOffset? nextAttempt = null)
    {
        var row = new NotificationOutboxEntity
        {
            NotificationOutboxId = Guid.NewGuid(),
            IdempotencyKey = $"seed:{Guid.NewGuid():N}",
            Channel = channel,
            Category = "Transactional",
            TemplateKey = NotificationTemplateKeys.Test,
            Locale = "ru",
            RecipientAddress = "player@example.com",
            Subject = "s",
            BodyText = "t",
            BodyHtml = "<p>t</p>",
            Status = NotificationOutboxStatus.Pending,
            AttemptCount = attemptCount,
            NextAttemptUtc = nextAttempt ?? Now.AddMinutes(-1),
            CreatedUtc = Now,
        };
        db.NotificationOutbox.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task RunAsync_DeliversDueRowAndMarksSent()
    {
        await using var db = CreateDb();
        var channel = new FakeChannel(NotificationChannel.Email, _ => ChannelResult.Sent());
        var runner = CreateRunner(db, channel);
        var seeded = await SeedAsync(db);

        var processed = await runner.RunAsync(max: 10, CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Single(channel.Sent);
        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Sent, row.Status);
        Assert.Equal(Now, row.SentUtc);
        Assert.Equal(1, row.AttemptCount);
        Assert.Equal(seeded.NotificationOutboxId, row.NotificationOutboxId);
    }

    [Fact]
    public async Task RunAsync_TransientFailureSchedulesRetryWithBackoff()
    {
        await using var db = CreateDb();
        var channel = new FakeChannel(NotificationChannel.Email, _ => ChannelResult.TransientFailure("smtp timeout"));
        var runner = CreateRunner(db, channel);
        await SeedAsync(db);

        await runner.RunAsync(max: 10, CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Pending, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.Equal(Now.AddMinutes(1), row.NextAttemptUtc);
        Assert.Contains("smtp timeout", row.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PermanentFailureFailsFast()
    {
        await using var db = CreateDb();
        var channel = new FakeChannel(NotificationChannel.Email, _ => ChannelResult.PermanentFailure("bad address"));
        var runner = CreateRunner(db, channel);
        await SeedAsync(db);

        await runner.RunAsync(max: 10, CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Failed, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.Contains("bad address", row.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_FailsAfterMaxAttempts()
    {
        await using var db = CreateDb();
        var channel = new FakeChannel(NotificationChannel.Email, _ => ChannelResult.TransientFailure("still down"));
        var runner = CreateRunner(db, channel);
        await SeedAsync(db, attemptCount: 2); // MaxAttempts = 3; this attempt makes it 3

        await runner.RunAsync(max: 10, CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Failed, row.Status);
        Assert.Equal(3, row.AttemptCount);
    }

    [Fact]
    public async Task RunAsync_MarksFailedWhenNoChannelRegisteredForRow()
    {
        await using var db = CreateDb();
        var channel = new FakeChannel(NotificationChannel.Email, _ => ChannelResult.Sent());
        var runner = CreateRunner(db, channel);
        await SeedAsync(db, channel: "Sms");

        await runner.RunAsync(max: 10, CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Failed, row.Status);
        Assert.Contains("Sms", row.LastError, StringComparison.Ordinal);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public async Task RunAsync_ReturnsZeroWhenNothingDue()
    {
        await using var db = CreateDb();
        var channel = new FakeChannel(NotificationChannel.Email, _ => ChannelResult.Sent());
        var runner = CreateRunner(db, channel);
        await SeedAsync(db, nextAttempt: Now.AddHours(1));

        var processed = await runner.RunAsync(max: 10, CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Empty(channel.Sent);
    }

    private sealed class FakeChannel(NotificationChannel channel, Func<NotificationOutboxEntity, ChannelResult> behaviour) : INotificationChannel
    {
        public List<NotificationOutboxEntity> Sent { get; } = [];

        public NotificationChannel Channel => channel;

        public Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
        {
            Sent.Add(row);
            return Task.FromResult(behaviour(row));
        }
    }
}
