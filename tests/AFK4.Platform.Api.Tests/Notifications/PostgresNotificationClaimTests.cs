using System.Data.Common;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>
/// При выкладке старый и новый контейнер API работают одновременно, и у обоих тикает отправка
/// уведомлений. Захват «прочитал и пометил» отдавал одну и ту же пачку обоим — игрок получал SMS
/// дважды. Барьер ниже заставляет обе стороны прочитать очередь ДО того, как любая из них запишет.
/// </summary>
public sealed class PostgresNotificationClaimTests
{
    [PostgresSessionFact]
    public async Task TwoDispatchersClaimingTogether_SplitTheBatch_NeverShareARow()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        var seeded = await SeedPendingAsync(database, count: 10);

        using var barrier = new Barrier(2);
        await using var first = database.CreateDbContext(new OutboxReadBarrier(barrier));
        await using var second = database.CreateDbContext(new OutboxReadBarrier(barrier));

        var firstClaim = Task.Run(() => new EfNotificationOutbox(first).ClaimDueAsync(database.Now, 10, CancellationToken.None));
        var secondClaim = Task.Run(() => new EfNotificationOutbox(second).ClaimDueAsync(database.Now, 10, CancellationToken.None));
        var a = (await firstClaim.WaitAsync(TimeSpan.FromSeconds(60))).Select(row => row.NotificationOutboxId).ToHashSet();
        var b = (await secondClaim.WaitAsync(TimeSpan.FromSeconds(60))).Select(row => row.NotificationOutboxId).ToHashSet();

        Assert.Empty(a.Intersect(b));
        Assert.Equal(seeded.ToHashSet(), a.Union(b).ToHashSet());
    }

    /// <summary>
    /// Процесс умер посреди отправки: строка осталась «отправляется». После аренды её подбирают
    /// снова, а пока аренда идёт — не трогают.
    /// </summary>
    [PostgresSessionFact]
    public async Task ARowLeftSendingByADeadDispatcher_IsReclaimedOnlyAfterTheLease()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        var abandoned = await SeedAsync(database, NotificationOutboxStatus.Sending, database.Now.AddMinutes(-1));
        var inFlight = await SeedAsync(database, NotificationOutboxStatus.Sending, database.Now.AddMinutes(4));

        await using var db = database.CreateDbContext();
        var claimed = await new EfNotificationOutbox(db).ClaimDueAsync(database.Now, 10, CancellationToken.None);

        var row = Assert.Single(claimed);
        Assert.Equal(abandoned, row.NotificationOutboxId);
        Assert.Equal(database.Now + EfNotificationOutbox.ClaimLease, row.NextAttemptUtc);
        Assert.NotEqual(inFlight, row.NotificationOutboxId);
    }

    private static async Task<List<Guid>> SeedPendingAsync(SessionStartPostgresFixture database, int count)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            ids.Add(await SeedAsync(database, NotificationOutboxStatus.Pending, database.Now.AddMinutes(-i - 1)));
        }

        return ids;
    }

    private static async Task<Guid> SeedAsync(SessionStartPostgresFixture database, string status, DateTimeOffset nextAttemptUtc)
    {
        await using var db = database.CreateDbContext();
        var id = Guid.NewGuid();
        db.NotificationOutbox.Add(new NotificationOutboxEntity
        {
            NotificationOutboxId = id,
            IdempotencyKey = $"claim-test-{id:N}",
            Channel = "Sms",
            Category = "transactional",
            TemplateKey = "test",
            Locale = "ru",
            RecipientAddress = "+992900000000",
            Subject = string.Empty,
            BodyText = "Код 123456",
            Status = status,
            NextAttemptUtc = nextAttemptUtc,
            CreatedUtc = database.Now.AddMinutes(-10)
        });
        await db.SaveChangesAsync();
        return id;
    }

    private sealed class OutboxReadBarrier(Barrier barrier) : DbCommandInterceptor
    {
        private bool tripped;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!tripped && command.CommandText.Contains("notification_outbox", StringComparison.OrdinalIgnoreCase))
            {
                tripped = true;
                barrier.SignalAndWait(TimeSpan.FromSeconds(30));
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
