using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Reservations;

/// <summary>
/// Куда можно перенести бронь.
///
/// Панель предлагала только места, свободные прямо сейчас, даже если бронь на завтра: занятое
/// сейчас, но свободное завтра место было недоступно, а свободное сейчас и занятое завтра другой
/// бронью — предлагалось и отклонялось сервером. Список считается на окно самой брони и тем же
/// правилом, которым перенос её принимает.
/// </summary>
public sealed class ReservationFreeSeatsTests
{
    private static readonly Guid Actor = Guid.Parse("d4444444-4444-4444-4444-444444444444");

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    [Fact]
    public async Task TomorrowsBooking_SeesTheHallAsItWillBeTomorrow()
    {
        var options = NewOptions();
        var scenario = FreeSeatsScenario.Default;
        await using (var seedDb = new PlatformDbContext(options))
        {
            await scenario.SeedAsync(seedDb, withLayout: true);
        }

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, new FixedTimeProvider(scenario.Now));

        var result = await service.FindFreeSeatsAsync(
            scenario.OrganizationId,
            scenario.BranchId,
            scenario.TomorrowStart,
            scenario.TomorrowEnd,
            scenario.MovedReservationId,
            CancellationToken.None);

        Assert.Equal(scenario.TomorrowStart, result.StartsAtUtc);
        Assert.Equal(scenario.TomorrowEnd, result.EndsAtUtc);
        Assert.Equal(scenario.FreeTomorrow.OrderBy(id => id), result.FreeSeatIds.OrderBy(id => id));
    }

    [Fact]
    public async Task BookingThatHasStarted_SeesEveryLiveSessionAsBusy()
    {
        var options = NewOptions();
        var scenario = FreeSeatsScenario.Default;
        await using (var seedDb = new PlatformDbContext(options))
        {
            await scenario.SeedAsync(seedDb, withLayout: true);
        }

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, new FixedTimeProvider(scenario.Now));

        var result = await service.FindFreeSeatsAsync(
            scenario.OrganizationId,
            scenario.BranchId,
            scenario.Now.AddMinutes(-30),
            scenario.Now.AddMinutes(30),
            excludedReservationId: null,
            CancellationToken.None);

        Assert.Equal(scenario.FreeNow.OrderBy(id => id), result.FreeSeatIds.OrderBy(id => id));
    }

    // Главное свойство списка: перенос на любое место из него проходит, на любое другое — нет.
    // Проверяется настоящим переносом, а не повтором того же запроса.
    [Fact]
    public async Task EverySeatInTheList_AcceptsTheMove_AndNoOtherSeatDoes()
    {
        var scenario = FreeSeatsScenario.Default;
        foreach (var seatId in scenario.AllSeatIds)
        {
            var options = NewOptions();
            await using (var seedDb = new PlatformDbContext(options))
            {
                await scenario.SeedAsync(seedDb, withLayout: true);
            }

            await using var db = new PlatformDbContext(options);
            var service = new EfReservationService(db, new FixedTimeProvider(scenario.Now));
            var free = await service.FindFreeSeatsAsync(
                scenario.OrganizationId,
                scenario.BranchId,
                scenario.TomorrowStart,
                scenario.TomorrowEnd,
                scenario.MovedReservationId,
                CancellationToken.None);

            var moved = await service.UpdateAsync(
                scenario.MovedReservationId,
                Actor,
                scenario.MoveTo(seatId),
                CancellationToken.None);

            Assert.True(
                free.FreeSeatIds.Contains(seatId) == moved.Succeeded,
                $"Seat {scenario.NameOf(seatId)}: listed={free.FreeSeatIds.Contains(seatId)}, move={moved.Succeeded} ({moved.Error})");
        }
    }

    [Fact]
    public async Task OtherBranchesAndOrganizations_DoNotLeakIntoTheList()
    {
        var options = NewOptions();
        var scenario = FreeSeatsScenario.Default;
        await using (var seedDb = new PlatformDbContext(options))
        {
            await scenario.SeedAsync(seedDb, withLayout: true);
            seedDb.Seats.Add(new SeatEntity
            {
                SeatId = Guid.NewGuid(), OrganizationId = scenario.OrganizationId, BranchId = Guid.NewGuid(),
                ZoneId = scenario.ZoneId, Name = "PC-99", SortOrder = 99, CreatedAtUtc = scenario.Now
            });
            seedDb.Seats.Add(new SeatEntity
            {
                SeatId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), BranchId = scenario.BranchId,
                ZoneId = scenario.ZoneId, Name = "PC-98", SortOrder = 98, CreatedAtUtc = scenario.Now
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, new FixedTimeProvider(scenario.Now));
        var result = await service.FindFreeSeatsAsync(
            scenario.OrganizationId,
            scenario.BranchId,
            scenario.TomorrowStart,
            scenario.TomorrowEnd,
            scenario.MovedReservationId,
            CancellationToken.None);

        Assert.All(result.FreeSeatIds, seatId => Assert.Contains(seatId, scenario.AllSeatIds));
    }
}

/// <summary>
/// Зал на семь мест в 14:00 и бронь на завтра 18:00–19:00, которую переносят. Общий для
/// in-memory и PostgreSQL: сравнение времени в запросе провайдеры переводят по-разному.
/// </summary>
internal sealed class FreeSeatsScenario
{
    public static FreeSeatsScenario Default => new();

    public Guid OrganizationId { get; init; } = Guid.Parse("71111111-1111-4111-8111-111111111111");
    public Guid BranchId { get; init; } = Guid.Parse("72222222-2222-4222-8222-222222222222");
    public Guid ZoneId { get; init; } = Guid.Parse("73333333-3333-4333-8333-333333333333");
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Parse("2026-07-14T14:00:00Z");

    public DateTimeOffset TomorrowStart => Now.AddDays(1).AddHours(4);
    public DateTimeOffset TomorrowEnd => TomorrowStart.AddHours(1);

    /// <summary>Здесь стоит сама переносимая бронь: своё место она не занимает.</summary>
    public Guid OwnSeat { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    /// <summary>Сейчас играют по оплаченному времени до 16:00 — завтра место свободно.</summary>
    public Guid BusyUntilToday { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    /// <summary>Сейчас сидит гость без конца сессии: в будущее такая сессия не переносится.</summary>
    public Guid OpenWalkIn { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    /// <summary>Сейчас свободно, но завтра на 18:30 здесь чужая бронь.</summary>
    public Guid BookedTomorrow { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    /// <summary>Завтра здесь была бронь, но её отменили.</summary>
    public Guid CancelledTomorrow { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000005");
    /// <summary>Сейчас играют по времени, оплаченному до завтра 18:30.</summary>
    public Guid PaidPastTomorrowStart { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000006");
    /// <summary>Оплаченное время кончилось в 13:15, но сессия ещё идёт.</summary>
    public Guid OverranSession { get; } = Guid.Parse("a0000000-0000-4000-8000-000000000007");

    public Guid MovedReservationId { get; } = Guid.Parse("b0000000-0000-4000-8000-000000000001");

    public IReadOnlyList<Guid> AllSeatIds =>
        [OwnSeat, BusyUntilToday, OpenWalkIn, BookedTomorrow, CancelledTomorrow, PaidPastTomorrowStart, OverranSession];

    public IReadOnlyList<Guid> FreeTomorrow =>
        [OwnSeat, BusyUntilToday, OpenWalkIn, CancelledTomorrow, OverranSession];

    public IReadOnlyList<Guid> FreeNow =>
        [OwnSeat, BookedTomorrow, CancelledTomorrow];

    public string NameOf(Guid seatId) => $"PC-{AllSeatIds.ToList().IndexOf(seatId) + 1:00}";

    public UpdateReservationRequest MoveTo(Guid seatId) => new(
        OrganizationId,
        ExpectedVersion: 1,
        PlayerAccountId: null,
        SeatId: seatId,
        CustomerName: null,
        PhoneNumber: null,
        StartsAtUtc: null,
        DurationMinutes: null,
        Source: null,
        Note: "перенос");

    /// <param name="withLayout">
    /// Зал с местами. PostgreSQL-фикстура заводит организацию, филиал и зал сама.
    /// </param>
    public async Task SeedAsync(PlatformDbContext db, bool withLayout)
    {
        if (withLayout)
        {
            db.Zones.Add(new ZoneEntity
            {
                ZoneId = ZoneId, OrganizationId = OrganizationId, BranchId = BranchId, Name = "Зал A",
                SortOrder = 1, CreatedAtUtc = Now
            });
        }

        foreach (var seatId in AllSeatIds)
        {
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId, OrganizationId = OrganizationId, BranchId = BranchId, ZoneId = ZoneId,
                Name = NameOf(seatId), SortOrder = AllSeatIds.ToList().IndexOf(seatId), CreatedAtUtc = Now
            });
        }

        db.Reservations.Add(Reservation(MovedReservationId, OwnSeat, TomorrowStart, TomorrowEnd, ReservationStateNames.Confirmed));
        db.Reservations.Add(Reservation(Guid.NewGuid(), BookedTomorrow, TomorrowStart.AddMinutes(30), TomorrowEnd.AddMinutes(30), ReservationStateNames.Pending));
        db.Reservations.Add(Reservation(Guid.NewGuid(), CancelledTomorrow, TomorrowStart, TomorrowEnd, ReservationStateNames.Cancelled));

        db.Sessions.Add(Session(BusyUntilToday, Now.AddHours(-1), Now.AddHours(2)));
        db.Sessions.Add(Session(OpenWalkIn, Now.AddHours(-1), endsAtUtc: null));
        db.Sessions.Add(Session(PaidPastTomorrowStart, Now.AddHours(-1), TomorrowStart.AddMinutes(30)));
        db.Sessions.Add(Session(OverranSession, Now.AddHours(-3), Now.AddMinutes(-45)));

        await db.SaveChangesAsync();
    }

    private ReservationEntity Reservation(
        Guid reservationId,
        Guid seatId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string state) => new()
    {
        ReservationId = reservationId,
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        SeatId = seatId,
        CustomerName = "Гость",
        StartsAtUtc = startsAtUtc,
        EndsAtUtc = endsAtUtc,
        State = state,
        Source = ReservationSourceNames.Operator,
        Note = string.Empty,
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now,
        CancelledAtUtc = state == ReservationStateNames.Cancelled ? Now : null,
        CancelReason = state == ReservationStateNames.Cancelled ? "передумал" : string.Empty,
        Version = 1
    };

    private SessionEntity Session(Guid seatId, DateTimeOffset startedAtUtc, DateTimeOffset? endsAtUtc) => new()
    {
        SessionId = Guid.NewGuid(),
        OrganizationId = OrganizationId,
        BranchId = BranchId,
        SeatId = seatId,
        DeviceId = Guid.NewGuid(),
        CreatedByStaffUserId = Guid.Parse("d4444444-4444-4444-4444-444444444444"),
        PlayerKind = "guest",
        TariffRuleVersionId = "guest",
        State = SessionStateNames.Active,
        RequestedAtUtc = startedAtUtc,
        StartedAtUtc = startedAtUtc,
        EndsAtUtc = endsAtUtc,
        UpdatedAtUtc = startedAtUtc
    };
}
