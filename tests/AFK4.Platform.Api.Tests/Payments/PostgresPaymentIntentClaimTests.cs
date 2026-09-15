using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Tests.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Payments;

/// <summary>
/// Отмена заявки на пополнение против её завершения на стойке — настоящая одновременность, на
/// настоящей PostgreSQL.
///
/// Обе стороны читали состояние, решали и писали своё. Порядок «стойка зачислила → отмена
/// записала cancelled» оставлял кошелёк пополненным при заявке, помеченной отменённой: в списке
/// игрока отмена, на кошельке деньги. In-memory провайдер этого не поймает — он условного
/// обновления не исполняет, и параллельные записи в нём не конкурируют.
/// </summary>
public sealed class PostgresPaymentIntentClaimTests
{
    private static readonly Guid IntentId = Guid.Parse("b1111111-1111-4111-8111-111111111111");

    [PostgresSessionFact]
    public async Task ConcurrentCancelAndFulfil_LeaveExactlyOneWinner()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedPendingIntentAsync(database);

        await using var fulfilDb = database.CreateDbContext();
        await using var cancelDb = database.CreateDbContext();
        var forFulfil = await LoadAsync(fulfilDb);
        var forCancel = await LoadAsync(cancelDb);

        // Обе стороны уже прочитали «ожидает» — ровно тот зазор, в котором терялось состояние.
        var outcomes = await Task.WhenAll(
            PaymentIntentClaim.TryMoveFromPendingAsync(
                fulfilDb, forFulfil, PaymentIntentClaim.Fulfilled, database.Now, CancellationToken.None),
            PaymentIntentClaim.TryMoveFromPendingAsync(
                cancelDb, forCancel, PaymentIntentClaim.Cancelled, null, CancellationToken.None))
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Single(outcomes, won => won);

        await using var readDb = database.CreateDbContext();
        var stored = await LoadAsync(readDb);
        var expected = outcomes[0] ? PaymentIntentClaim.Fulfilled : PaymentIntentClaim.Cancelled;
        // Записанное состояние принадлежит победителю, а не тому, кто писал последним.
        Assert.Equal(expected, stored.State);
    }

    // Проигравшая сторона обязана узнать о проигрыше: иначе она ответит «готово» на то, чего не
    // сделала, и стойка зачислит деньги под отменённую заявку.
    [PostgresSessionFact]
    public async Task ClaimAfterAnotherClaim_ReportsTheLoss()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedPendingIntentAsync(database);
        await using var db = database.CreateDbContext();
        var intent = await LoadAsync(db);

        var first = await PaymentIntentClaim.TryMoveFromPendingAsync(
            db, intent, PaymentIntentClaim.Cancelled, null, CancellationToken.None);
        await using var secondDb = database.CreateDbContext();
        var second = await PaymentIntentClaim.TryMoveFromPendingAsync(
            secondDb, await LoadAsync(secondDb), PaymentIntentClaim.Fulfilled, database.Now, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
    }

    // Зачисление не состоялось — заявка возвращается в «ожидает», иначе её нельзя ни повторить,
    // ни отменить: она застряла бы в «завершено» без денег.
    [PostgresSessionFact]
    public async Task Release_ReturnsAClaimedIntentToPending()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedPendingIntentAsync(database);
        await using var db = database.CreateDbContext();
        var intent = await LoadAsync(db);
        await PaymentIntentClaim.TryMoveFromPendingAsync(
            db, intent, PaymentIntentClaim.Fulfilled, database.Now, CancellationToken.None);

        var released = await PaymentIntentClaim.TryReleaseAsync(db, intent, CancellationToken.None);

        Assert.True(released);
        await using var readDb = database.CreateDbContext();
        var stored = await LoadAsync(readDb);
        Assert.Equal(PaymentIntentClaim.Pending, stored.State);
        Assert.Null(stored.FulfilledAtUtc);
    }

    // Отменённую заявку освобождать нечего: release трогает только то, что сам занял.
    [PostgresSessionFact]
    public async Task Release_DoesNotResurrectACancelledIntent()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedPendingIntentAsync(database);
        await using var db = database.CreateDbContext();
        var intent = await LoadAsync(db);
        await PaymentIntentClaim.TryMoveFromPendingAsync(
            db, intent, PaymentIntentClaim.Cancelled, null, CancellationToken.None);

        var released = await PaymentIntentClaim.TryReleaseAsync(db, intent, CancellationToken.None);

        Assert.False(released);
        await using var readDb = database.CreateDbContext();
        Assert.Equal(PaymentIntentClaim.Cancelled, (await LoadAsync(readDb)).State);
    }

    private static Task<SessionStartPostgresFixture> CreateDatabaseAsync() =>
        SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);

    private static Task<PaymentIntentEntity> LoadAsync(PlatformDbContext db) =>
        db.PaymentIntents.SingleAsync(intent => intent.PaymentIntentId == IntentId);

    private static async Task SeedPendingIntentAsync(SessionStartPostgresFixture database)
    {
        await using var db = database.CreateDbContext();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = IntentId,
            PlayerAccountId = database.PlayerAccountId,
            OrganizationId = database.OrganizationId,
            BranchId = database.BranchId,
            AmountMinorUnits = 5_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = PaymentIntentClaim.Pending,
            Method = "counter",
            CreatedAtUtc = database.Now
        });
        await db.SaveChangesAsync();
    }
}
