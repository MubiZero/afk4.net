using System.Data.Common;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AFK4.Platform.Api.Tests.Reservations;

/// <summary>
/// Две одновременные брони на последнюю машину.
///
/// Проверка вместимости читает, сколько мест обещано, и вставляет бронь. Порознь это классическая
/// гонка: обе брони читают «свободно», обе проходят проверку, обе вставляются — зал на одну машину
/// раздаёт два обещания и замораживает деньги дважды. In-memory провайдер такого не покажет:
/// изоляции у него нет вовсе, поэтому проверять это можно только на настоящей PostgreSQL.
///
/// Барьер ниже заставляет обе стороны прочитать вместимость ДО того, как любая из них вставит
/// строку, — то самое переплетение, ради которого тест и написан. Без него порядок решает
/// планировщик, и опасный случай выпадал бы через раз.
/// </summary>
public sealed class PostgresBookingCapacityConcurrencyTests
{
    [PostgresSessionFact]
    public async Task TwoBookingsForTheLastMachine_LeaveExactlyOnePromise()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();

        // Тест про гонку за последнюю машину, а не про правила приёма гостей: филиал берёт брони
        // без предоплаты, иначе обе стороны отказались бы ещё до проверки вместимости.
        await using (var settingsDb = database.CreateDbContext())
        {
            await BranchBookingSettingsTestData.AcceptAnyGuestAsync(
                settingsDb, database.OrganizationId, database.BranchId, database.Now);
        }

        using var barrier = new Barrier(2);
        await using var firstDb = database.CreateDbContext(new ReservationReadBarrier(barrier));
        await using var secondDb = database.CreateDbContext(new ReservationReadBarrier(barrier));

        var startsAt = database.Now.AddHours(5);
        var request = new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null, null);

        var results = await Task.WhenAll(
            BookAsync(firstDb, database, request),
            BookAsync(secondDb, database, request)).WaitAsync(TimeSpan.FromSeconds(60));

        // Зал на одну машину: ровно одно обещание, второму честный отказ.
        Assert.Single(results, result => result.Succeeded);
        var loser = Assert.Single(results, result => !result.Succeeded);
        Assert.True(loser.Conflict);
        Assert.Equal("no_seats_available", loser.Error);

        await using var verifyDb = database.CreateDbContext();
        Assert.Equal(1, verifyDb.Reservations.Count());
    }

    private static Task<ReservationServiceResult<ReservationDto>> BookAsync(
        PlatformDbContext db,
        SessionStartPostgresFixture database,
        CreatePlayerReservationRequest request) =>
        Task.Run(() => new EfReservationService(db, new FixedTimeProvider(database.Now)).CreateOnlineAsync(
            database.PlayerAccountId,
            database.OrganizationId,
            database.BranchId,
            request,
            CancellationToken.None));

    /// <summary>
    /// Отпускает обе стороны только после того, как каждая прочитала таблицу броней: до вставки,
    /// но уже внутри своей транзакции.
    /// </summary>
    private sealed class ReservationReadBarrier(Barrier barrier) : DbCommandInterceptor
    {
        private bool tripped;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!tripped && command.CommandText.Contains("reservations", StringComparison.OrdinalIgnoreCase))
            {
                tripped = true;
                // С таймаутом: если вторая сторона до чтения не дошла, тест обязан упасть, а не
                // подвесить весь прогон.
                barrier.SignalAndWait(TimeSpan.FromSeconds(30));
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
