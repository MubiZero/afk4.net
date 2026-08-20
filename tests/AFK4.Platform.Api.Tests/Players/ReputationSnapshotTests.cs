using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Третий рубеж приватности: считает не запрос, а фон. Клуб, опрашивающий живой счётчик каждую
/// минуту, увидел бы «+1» ровно в ту секунду, когда человек сел за ПК напротив, и узнал бы, где
/// тот играет, не получив ни одного названия клуба. Суточная задержка делает такую корреляцию
/// бессмысленной, а точность цифры сохраняет.
/// </summary>
public sealed class ReputationSnapshotTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T03:00:00Z");

    [Fact]
    public async Task Runner_CountsVisitsAndNoShowsAcrossEveryClubOfOnePerson()
    {
        var options = NewOptions();
        var personId = Guid.NewGuid();
        var firstClubAccount = Guid.NewGuid();
        var secondClubAccount = Guid.NewGuid();

        await using (var db = new PlatformDbContext(options))
        {
            AddPerson(db, personId, "+992900000301");
            AddAccount(db, firstClubAccount, personId);
            AddAccount(db, secondClubAccount, personId);

            AddSession(db, firstClubAccount, SessionStateNames.Ended);
            AddSession(db, firstClubAccount, SessionStateNames.Reconciled);
            AddSession(db, secondClubAccount, SessionStateNames.Ended);
            // Живая сессия визитом ещё не стала: человек за ПК прямо сейчас.
            AddSession(db, secondClubAccount, SessionStateNames.Active);

            AddReservation(db, secondClubAccount, ReservationStateNames.Cancelled, "no-show");
            // Отмена по инициативе игрока и молчание клуба неявкой не считаются.
            AddReservation(db, firstClubAccount, ReservationStateNames.Cancelled, "player-cancelled");
            AddReservation(db, firstClubAccount, ReservationStateNames.Cancelled, "request-expired");
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await RunAsync(options, Now));

        await using (var db = new PlatformDbContext(options))
        {
            var snapshot = await db.PlatformReputationSnapshots.SingleAsync();
            Assert.Equal(personId, snapshot.PlatformPersonId);
            Assert.Equal(3, snapshot.NetworkVisits);
            Assert.Equal(1, snapshot.NetworkNoShows);
            Assert.Equal(Now, snapshot.CalculatedAtUtc);
        }
    }

    /// <summary>Счёт без личности принадлежит гостю со стойки: в сетевую репутацию он не входит.</summary>
    [Fact]
    public async Task Runner_IgnoresClubOnlyAccounts()
    {
        var options = NewOptions();
        var clubOnlyAccount = Guid.NewGuid();

        await using (var db = new PlatformDbContext(options))
        {
            AddAccount(db, clubOnlyAccount, platformPersonId: null);
            AddSession(db, clubOnlyAccount, SessionStateNames.Ended);
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await RunAsync(options, Now));

        await using (var db = new PlatformDbContext(options))
        {
            Assert.Empty(await db.PlatformReputationSnapshots.ToListAsync());
        }
    }

    [Fact]
    public async Task Runner_RewritesTheSnapshotOnEveryPass()
    {
        var options = NewOptions();
        var personId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        await using (var db = new PlatformDbContext(options))
        {
            AddPerson(db, personId, "+992900000302");
            AddAccount(db, accountId, personId);
            AddSession(db, accountId, SessionStateNames.Ended);
            await db.SaveChangesAsync();
        }

        await RunAsync(options, Now);

        await using (var db = new PlatformDbContext(options))
        {
            AddSession(db, accountId, SessionStateNames.Ended);
            await db.SaveChangesAsync();
        }

        await RunAsync(options, Now.AddDays(1));

        await using (var db = new PlatformDbContext(options))
        {
            var snapshot = await db.PlatformReputationSnapshots.SingleAsync();
            Assert.Equal(2, snapshot.NetworkVisits);
            Assert.Equal(Now.AddDays(1), snapshot.CalculatedAtUtc);
        }
    }

    /// <summary>
    /// Два запроса подряд после визита человека в соседний клуб дают одно и то же число. Это и
    /// есть защита: разница между двумя ответами не должна выдавать чужой вечер.
    /// </summary>
    [Fact]
    public async Task TwoRequestsInARow_SurviveAVisitToTheClubNextDoor()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var personId = await ReputationTestData.AddPersonAsync(factory, "+992900000303");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, personId);
        await ReputationTestData.AddSnapshotAsync(factory, personId, visits: 14, noShows: 0);

        var (otherOrganizationId, otherBranchId) = await ReputationTestData.AddOtherClubAsync(factory);
        var neighbourAccount = await ReputationTestData.AddAccountAsync(
            factory, otherOrganizationId, otherBranchId, personId);

        var before = await ReadAsync(client, personId);
        await ReputationTestData.AddEndedSessionAsync(
            factory, otherOrganizationId, otherBranchId, neighbourAccount);
        var after = await ReadAsync(client, personId);

        Assert.Equal(14, before.NetworkVisits);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// «На когда посчитано» — величина общая для сети, а не личная. Иначе по времени пересчёта
    /// конкретного человека можно было бы понять, что у него вообще есть что пересчитывать.
    /// </summary>
    [Fact]
    public async Task AsOfTime_IsTheSameForEveryone()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var veteranId = await ReputationTestData.AddPersonAsync(factory, "+992900000304");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, veteranId);
        await ReputationTestData.AddSnapshotAsync(
            factory, veteranId, visits: 14, noShows: 0, calculatedAtUtc: ReputationTestData.SnapshotAt);

        // Строка, застрявшая с прошлого прохода: если бы ответ брал время из строки человека,
        // по нему было бы видно, кого пересчитывали, а кого нет.
        var staleId = await ReputationTestData.AddPersonAsync(factory, "+992900000306");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, staleId);
        await ReputationTestData.AddSnapshotAsync(
            factory, staleId, visits: 2, noShows: 0,
            calculatedAtUtc: ReputationTestData.SnapshotAt.AddDays(-3));

        var newcomerId = await ReputationTestData.AddPersonAsync(factory, "+992900000305");
        await ReputationTestData.AddAccountAsync(factory, TestIds.OrganizationId, TestIds.BranchId, newcomerId);

        var veteran = await ReadAsync(client, veteranId);
        var stale = await ReadAsync(client, staleId);
        var newcomer = await ReadAsync(client, newcomerId);

        Assert.Equal(ReputationTestData.SnapshotAt, veteran.CalculatedAtUtc);
        Assert.Equal(veteran.CalculatedAtUtc, stale.CalculatedAtUtc);
        Assert.Equal(veteran.CalculatedAtUtc, newcomer.CalculatedAtUtc);
        Assert.Equal(0, newcomer.NetworkVisits);
    }

    private static async Task<PlayerReputationDto> ReadAsync(HttpClient client, Guid personId)
    {
        var response = await client.GetAsync(ReputationTestData.ReputationRoute(personId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlayerReputationDto>();
        Assert.NotNull(body);
        return body;
    }

    private static async Task<int> RunAsync(DbContextOptions<PlatformDbContext> options, DateTimeOffset now)
    {
        await using var db = new PlatformDbContext(options);
        return await new ReputationSnapshotRunner(db, new FixedTimeProvider(now)).RunOnceAsync(CancellationToken.None);
    }

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static void AddPerson(PlatformDbContext db, Guid personId, string phone) =>
        db.PlatformPersons.Add(new PlatformPersonEntity
        {
            PlatformPersonId = personId,
            PhoneNumber = phone,
            DisplayName = "Фаррух",
            IsActive = true,
            CreatedAtUtc = Now.AddYears(-1),
            UpdatedAtUtc = Now.AddYears(-1)
        });

    private static void AddAccount(PlatformDbContext db, Guid accountId, Guid? platformPersonId) =>
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = accountId,
            OrganizationId = Guid.NewGuid(),
            HomeBranchId = Guid.NewGuid(),
            PlatformPersonId = platformPersonId,
            DisplayName = "Карточка клуба",
            IsActive = true,
            CreatedAtUtc = Now.AddYears(-1)
        });

    private static void AddSession(PlatformDbContext db, Guid accountId, string state) =>
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            PlayerKind = "account",
            PlayerAccountId = accountId,
            State = state,
            RequestedAtUtc = Now.AddDays(-2),
            StartedAtUtc = Now.AddDays(-2),
            EndedAtUtc = state == SessionStateNames.Active ? null : Now.AddDays(-2).AddHours(2),
            UpdatedAtUtc = Now.AddDays(-2).AddHours(2)
        });

    private static void AddReservation(PlatformDbContext db, Guid accountId, string state, string cancelReason) =>
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            PlayerAccountId = accountId,
            CustomerName = "Фаррух",
            StartsAtUtc = Now.AddDays(-3),
            EndsAtUtc = Now.AddDays(-3).AddHours(1),
            State = state,
            Source = ReservationSourceNames.Online,
            CancelReason = cancelReason,
            CancelledAtUtc = Now.AddDays(-3),
            CreatedAtUtc = Now.AddDays(-4),
            UpdatedAtUtc = Now.AddDays(-3)
        });

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
