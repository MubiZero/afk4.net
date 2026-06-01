using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class NotificationOutboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static NotificationOutboxEntity NewRow(
        string key,
        string status = NotificationOutboxStatus.Pending,
        DateTimeOffset? nextAttempt = null) => new()
    {
        NotificationOutboxId = Guid.NewGuid(),
        IdempotencyKey = key,
        Channel = "Email",
        Category = "Transactional",
        TemplateKey = NotificationTemplateKeys.Test,
        Locale = "ru",
        RecipientAddress = "player@example.com",
        Subject = "s",
        BodyText = "t",
        BodyHtml = "<p>t</p>",
        Status = status,
        NextAttemptUtc = nextAttempt ?? Now,
        CreatedUtc = Now,
    };

    [Fact]
    public async Task AddIfAbsentAsync_InsertsNewRow()
    {
        await using var db = CreateDb();
        var outbox = new EfNotificationOutbox(db);

        var result = await outbox.AddIfAbsentAsync(NewRow("k1"), CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal("k1", result.Row.IdempotencyKey);
        Assert.Equal(1, await db.NotificationOutbox.CountAsync());
    }

    [Fact]
    public async Task AddIfAbsentAsync_IsIdempotentOnKey()
    {
        await using var db = CreateDb();
        var outbox = new EfNotificationOutbox(db);
        var first = await outbox.AddIfAbsentAsync(NewRow("dup"), CancellationToken.None);

        var second = await outbox.AddIfAbsentAsync(NewRow("dup"), CancellationToken.None);

        Assert.False(second.Created);
        Assert.Equal(first.Row.NotificationOutboxId, second.Row.NotificationOutboxId);
        Assert.Equal(1, await db.NotificationOutbox.CountAsync());
    }

    [Fact]
    public async Task ClaimDueAsync_ReturnsOnlyDuePendingRowsMarkedSending()
    {
        await using var db = CreateDb();
        var outbox = new EfNotificationOutbox(db);
        await outbox.AddIfAbsentAsync(NewRow("due", nextAttempt: Now.AddMinutes(-1)), CancellationToken.None);
        await outbox.AddIfAbsentAsync(NewRow("future", nextAttempt: Now.AddMinutes(10)), CancellationToken.None);
        await outbox.AddIfAbsentAsync(
            NewRow("already-sent", status: NotificationOutboxStatus.Sent, nextAttempt: Now.AddMinutes(-1)),
            CancellationToken.None);

        var claimed = await outbox.ClaimDueAsync(Now, max: 10, CancellationToken.None);

        var row = Assert.Single(claimed);
        Assert.Equal("due", row.IdempotencyKey);
        Assert.Equal(NotificationOutboxStatus.Sending, row.Status);
    }

    [Fact]
    public async Task ClaimDueAsync_RespectsMaxBatchSize()
    {
        await using var db = CreateDb();
        var outbox = new EfNotificationOutbox(db);
        for (var i = 0; i < 5; i++)
        {
            await outbox.AddIfAbsentAsync(NewRow($"k{i}", nextAttempt: Now.AddMinutes(-1)), CancellationToken.None);
        }

        var claimed = await outbox.ClaimDueAsync(Now, max: 2, CancellationToken.None);

        Assert.Equal(2, claimed.Count);
    }
}
