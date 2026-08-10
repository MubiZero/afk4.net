using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Sessions;

/// <summary>
/// Гонка двух стартов на одно место разрешается в Postgres двумя разными способами. Уникальный
/// частичный индекс (проигравший видит вставку соперника) покрыт
/// <see cref="Reservations.PostgresReservationStartConcurrencyTests"/>. Здесь — вторая развязка:
/// Serializable отменяет проигравшую транзакцию с 40001 ещё до вставки. Какая именно сработает,
/// решает планировщик, поэтому «настоящая» гонка ловила её через раз и годами выглядела флаком CI;
/// эти тесты воспроизводят её детерминированно, подставляя отказ вместо ожидания везения.
/// </summary>
public sealed class PostgresSessionStartSerializationRetryTests
{
    [PostgresSessionFact]
    public async Task TransientSerializationFailure_RetriesAndStartsTheSessionOnce()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await using var db = database.CreateDbContext();
        var harness = SessionStartHarness.Create(database, db, failures: 1);

        var result = await harness.Service.StartGuestSessionAsync(
            database.BranchId,
            database.StaffUserId,
            Request(database, "serialization-retry-transient"),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, harness.Workflow.StageCalls);

        // Откат первой попытки обязан быть полным: одна сессия и один комплект её следов, иначе
        // повтор дописал бы вторую аренду/проводку к уже частично сохранённому старту.
        await using var verification = database.CreateDbContext();
        Assert.Single(await verification.Sessions.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionLeases.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionEvents.AsNoTracking().ToListAsync());
        Assert.Single(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Single(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
    }

    [PostgresSessionFact]
    public async Task PersistentSerializationFailure_AnswersSeatOccupiedInsteadOfThrowing()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await using var db = database.CreateDbContext();
        var harness = SessionStartHarness.Create(database, db, failures: int.MaxValue);

        var result = await harness.Service.StartGuestSessionAsync(
            database.BranchId,
            database.StaffUserId,
            Request(database, "serialization-retry-persistent"),
            CancellationToken.None);

        // Соперник выигрывает раз за разом — это «место занято», а не пятисотка на кассе.
        Assert.False(result.Succeeded);
        Assert.True(result.Conflict);
        Assert.Equal("seat_occupied", result.Code);
        Assert.Equal(3, harness.Workflow.StageCalls);

        await using var verification = database.CreateDbContext();
        Assert.Empty(await verification.Sessions.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.LedgerEntries.AsNoTracking().ToListAsync());
        Assert.Empty(await verification.SessionCommandIdempotency.AsNoTracking().ToListAsync());
    }

    private static StartGuestSessionRequest Request(SessionStartPostgresFixture database, string key) =>
        new(
            database.OrganizationId,
            database.SeatId,
            TariffRuleVersionId: key,
            IdempotencyKey: key,
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            PlayerAccountId: database.PlayerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet);

    private sealed record SessionStartHarness(
        EfSessionCommandService Service,
        SerializationFailingWorkflow Workflow)
    {
        public static SessionStartHarness Create(
            SessionStartPostgresFixture database,
            PlatformDbContext db,
            int failures)
        {
            var dispatcher = new SavingCommandDispatchService(db);
            var lifecycle = new RecordingLifecycleNotifier();
            var timeProvider = new FixedTimeProvider(database.Now);
            var billing = new TrackingSessionBillingService(db, database.OrganizationId, database.BranchId);
            var workflow = new SerializationFailingWorkflow(
                new EfSessionStartWorkflow(
                    db,
                    dispatcher,
                    new FakeSessionLeaseSigner(),
                    timeProvider,
                    billing,
                    lifecycle,
                    new EfPlanLimitGuard(db)),
                failures);

            return new SessionStartHarness(
                new EfSessionCommandService(
                    db,
                    dispatcher,
                    new FakeSessionLeaseSigner(),
                    timeProvider,
                    billing,
                    lifecycle,
                    workflow),
                workflow);
        }
    }

    /// <summary>
    /// Стажирует старт как обычно, но первые <c>failures</c> попыток заканчивает тем самым
    /// отказом сериализации, который Postgres выдаёт проигравшему в гонке за место.
    /// </summary>
    private sealed class SerializationFailingWorkflow(ISessionStartWorkflow inner, int failures)
        : ISessionStartWorkflow
    {
        public int StageCalls { get; private set; }

        public async Task<SessionStartStage> StageAsync(
            Guid branchId,
            Guid actorStaffUserId,
            StartGuestSessionRequest request,
            bool actorCanApproveComp,
            CancellationToken cancellationToken)
        {
            StageCalls++;
            var stage = await inner.StageAsync(
                branchId,
                actorStaffUserId,
                request,
                actorCanApproveComp,
                cancellationToken);

            if (StageCalls <= failures)
            {
                throw new DbUpdateException(
                    "could not serialize access due to read/write dependencies among transactions",
                    new PostgresException(
                        "could not serialize access due to read/write dependencies among transactions",
                        "ERROR",
                        "ERROR",
                        PostgresErrorCodes.SerializationFailure));
            }

            return stage;
        }

        public Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken) =>
            inner.NotifyCommittedAsync(stage, cancellationToken);
    }
}
