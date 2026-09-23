using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Reservations;

namespace AFK4.Platform.Api.Tests.Reservations;

/// <summary>
/// Список мест под перенос и сам перенос — на настоящей PostgreSQL.
///
/// Правило сравнивает время сессий и броней с окном прямо в запросе, а бессрочная сессия — это
/// NULL в столбце конца. In-memory провайдер сравнивает NULL как C#, PostgreSQL — как SQL, и
/// именно здесь два провайдера расходятся молча.
/// </summary>
public sealed class PostgresReservationFreeSeatsTests
{
    private static readonly Guid Actor = Guid.Parse("d4444444-4444-4444-4444-444444444444");

    [PostgresSessionFact]
    public async Task FreeSeatsAndTheMoveItself_AgreeOnPostgres()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        var scenario = new FreeSeatsScenario
        {
            OrganizationId = database.OrganizationId,
            BranchId = database.BranchId,
            ZoneId = database.ZoneId,
            Now = database.Now
        };
        await using (var seedDb = database.CreateDbContext())
        {
            await scenario.SeedAsync(seedDb, withLayout: false);
        }

        await using var db = database.CreateDbContext();
        var service = new EfReservationService(db, new FixedTimeProvider(scenario.Now));

        var tomorrow = await service.FindFreeSeatsAsync(
            scenario.OrganizationId, scenario.BranchId, scenario.TomorrowStart, scenario.TomorrowEnd,
            scenario.MovedReservationId, CancellationToken.None);
        var now = await service.FindFreeSeatsAsync(
            scenario.OrganizationId, scenario.BranchId, scenario.Now.AddMinutes(-30), scenario.Now.AddMinutes(30),
            excludedReservationId: null, CancellationToken.None);

        // Место фикстуры пустое весь день и свободно в обоих окнах.
        Assert.Equal(
            scenario.FreeTomorrow.Append(database.SeatId).OrderBy(id => id),
            tomorrow.FreeSeatIds.OrderBy(id => id));
        Assert.Equal(
            scenario.FreeNow.Append(database.SeatId).OrderBy(id => id),
            now.FreeSeatIds.OrderBy(id => id));

        // Завтрашнюю бронь переносят на место, где сейчас сидит гость без конца сессии: список его
        // предложил, и сервер обязан перенос принять.
        var moved = await service.UpdateAsync(
            scenario.MovedReservationId, Actor, scenario.MoveTo(scenario.OpenWalkIn), CancellationToken.None);
        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal(scenario.OpenWalkIn, moved.Response!.SeatId);

        // А на место с чужой завтрашней бронью — нет, хотя сейчас оно свободно.
        var rejected = await service.UpdateAsync(
            scenario.MovedReservationId,
            Actor,
            scenario.MoveTo(scenario.BookedTomorrow) with { ExpectedVersion = moved.Response.Version },
            CancellationToken.None);
        Assert.True(rejected.Conflict);
    }
}
