using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Вместимость зала при брони из приложения.
///
/// Бронь из приложения приходит без места — его назначает клуб при посадке. Проверка пересечений
/// по месту для такой брони всегда молчала, и зал на две машины принимал сколько угодно броней на
/// один вечер. Здесь проверяется единственная форма вопроса, которая для брони без места вообще
/// имеет смысл: сколько машин есть и сколько уже обещано.
/// </summary>
public class PlayerBookingCapacityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Seeded(
        Guid OrgId,
        Guid BranchId,
        Guid PlayerId,
        string Phone,
        Guid TariffVersionId,
        IReadOnlyList<Guid> SeatIds);

    /// <summary>
    /// Зал на <paramref name="seatCount"/> машин: место без привязанного принятого игрового
    /// компьютера вместимостью не считается, поэтому сажать надо и места, и машины, и привязку.
    /// </summary>
    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        string pin,
        int seatCount,
        long walletMinorUnits = 100_000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var zone = Guid.NewGuid();
        var tariffId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        var phone = $"+99291000{(uint)player.GetHashCode() % 10_000:D4}";

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = org, Name = "Capacity Test Org", CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch, OrganizationId = org, Slug = $"b{branch:N}"[..12], Name = "CyberX на Айни",
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zone, OrganizationId = org, BranchId = branch, Name = "Общий зал", SortOrder = 1,
            CreatedAtUtc = Now
        });

        var seatIds = new List<Guid>(seatCount);
        for (var index = 0; index < seatCount; index++)
        {
            var seatId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            seatIds.Add(seatId);

            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId, OrganizationId = org, BranchId = branch, ZoneId = zone,
                Name = $"PC-{index + 1:00}", SortOrder = index, CreatedAtUtc = Now
            });
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = deviceId, OrganizationId = org, BranchId = branch,
                MachineName = $"PC-{index + 1:00}", DisplayName = $"PC-{index + 1:00}",
                DevicePublicKey = $"key-{deviceId:N}", Role = DeviceRoleNames.GamingPc,
                EnrollmentState = DeviceEnrollmentStateNames.Approved, EnrolledAtUtc = Now,
                // Машины выключены: ночью в зале не горит ничего, а бронируют как раз завтрашний
                // вечер. Вместимость «по онлайну» обнулила бы его целиком.
                IsOnline = false
            });
            db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(), OrganizationId = org, BranchId = branch,
                SeatId = seatId, DeviceId = deviceId, AttachedAtUtc = Now
            });
        }

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player, OrganizationId = org, HomeBranchId = branch, DisplayName = "Игрок",
            PhoneNumber = phone, PreferredLocale = "ru", MarketingOptIn = false, IsActive = true,
            CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = org, BranchId = branch, PlayerAccountId = player,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = walletMinorUnits, CurrencyCode = "TJS", CreatedAtUtc = Now
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId, OrganizationId = org, BranchId = branch, Name = "Дневной", IsActive = true,
            CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId, TariffId = tariffId, OrganizationId = org, BranchId = branch,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1, EffectiveFromUtc = Now.AddYears(-1)
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new Seeded(org, branch, player, phone, tariffVersionId, seatIds);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    /// <summary>Чужая бронь на то же окно — та, из-за которой мест и не остаётся.</summary>
    private static async Task PromiseSeatsAsync(
        PlatformApiFactory factory,
        Seeded branch,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int count,
        string state = ReservationStateNames.Confirmed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        for (var index = 0; index < count; index++)
        {
            db.Reservations.Add(new ReservationEntity
            {
                ReservationId = Guid.NewGuid(), OrganizationId = branch.OrgId, BranchId = branch.BranchId,
                SeatId = null, CustomerName = $"Гость {index + 1}", StartsAtUtc = startsAtUtc,
                EndsAtUtc = endsAtUtc, State = state, Source = ReservationSourceNames.Online,
                Note = string.Empty, CancelReason = string.Empty, CreatedAtUtc = Now, UpdatedAtUtc = Now
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Посаженная бронь: место занято живым человеком. Сессии при этом может не быть вовсе —
    /// SeatAsync её не создаёт.
    /// </summary>
    private static async Task SeatReservationAsync(
        PlatformApiFactory factory,
        Seeded branch,
        Guid seatId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = Guid.NewGuid(), OrganizationId = branch.OrgId, BranchId = branch.BranchId,
            SeatId = seatId, CustomerName = "Севший гость", StartsAtUtc = startsAtUtc, EndsAtUtc = endsAtUtc,
            State = ReservationStateNames.Seated, Source = ReservationSourceNames.Operator,
            Note = string.Empty, CancelReason = string.Empty, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<long> AvailableWalletAsync(PlatformApiFactory factory, Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.LedgerEntries
            .Where(entry =>
                entry.PlayerAccountId == playerId &&
                entry.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(entry => entry.AmountMinorUnits);
    }

    private static Task<HttpResponseMessage> BookAsync(
        HttpClient client, Seeded seeded, DateTimeOffset start, DateTimeOffset end) =>
        client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, start, end, null, seeded.TariffVersionId));

    [Fact]
    public async Task Booking_IsRefusedWhenEveryMachineIsAlreadyPromisedForThatEvening()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 2);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(2);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 2);

        var response = await BookAsync(client, seeded, start, end);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("no_seats_available", body!.Error);
        // Отказали до заморозки: кошелёк нетронут.
        Assert.Equal(100_000, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    [Fact]
    public async Task Booking_GoesThroughWhileOneMachineIsStillFree()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 2);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1);

        var response = await BookAsync(client, seeded, start, end);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(98_500, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    // Компания влезает целиком или не влезает вовсе: три места в зале, одно обещано — вчетвером
    // прийти уже нельзя, и узнать об этом надо сейчас, а не у стойки.
    [Fact]
    public async Task Group_IsRefusedWhenTheWholeCompanyDoesNotFit()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 3);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(6);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1);

        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(3, start, end, null, seeded.TariffVersionId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("no_seats_available", body!.Error);

        var mine = await client.GetFromJsonAsync<List<PlayerReservationDto>>("/api/me/reservations");
        Assert.Empty(mine!);
        Assert.Equal(100_000, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    [Fact]
    public async Task Group_FitsWhenThereIsExactlyEnoughRoomForIt()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 3);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(6);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1);

        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(2, start, end, null, seeded.TariffVersionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<PlayerReservationGroupDto>();
        Assert.Equal(2, group!.Reservations.Count);
    }

    // Полный вечер не должен закрывать утро: занятость считается по окну брони, а не «вообще».
    [Fact]
    public async Task AFullEvening_LeavesTheNextMorningBookable()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 2);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var evening = Now.AddHours(5);
        await PromiseSeatsAsync(factory, seeded, evening, evening.AddHours(3), count: 2);

        var morning = evening.AddHours(12);
        var response = await BookAsync(client, seeded, morning, morning.AddHours(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ACancelledPromise_StopsHoldingAMachine()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1, state: ReservationStateNames.Cancelled);

        var response = await BookAsync(client, seeded, start, end);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Сессия без запланированного конца длится «пока играет» и в завтрашний вечер не переносится.
    /// Иначе сегодняшний полный зал запретил бы бронировать вообще что угодно.
    /// </summary>
    [Fact]
    public async Task AnOpenEndedSessionToday_DoesNotBlockTomorrow()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        await StartSessionAsync(factory, seeded, seeded.SeatIds[0], endsAtUtc: null);

        var tomorrow = Now.AddHours(20);
        var response = await BookAsync(client, seeded, tomorrow, tomorrow.AddHours(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Оплаченное вперёд время — настоящее обещание машины на конкретные часы, и его надо считать.
    [Fact]
    public async Task ASessionWithAScheduledEnd_OccupiesTheMachineUntilThen()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(2);
        await StartSessionAsync(factory, seeded, seeded.SeatIds[0], endsAtUtc: start.AddHours(1));

        var response = await BookAsync(client, seeded, start, start.AddHours(1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("no_seats_available", body!.Error);
    }

    // Снятая с обслуживания машина исчезает и из вместимости, и из занятости — зал не «переполнен»
    // компьютерами, которых в нём уже нет.
    [Fact]
    public async Task ADetachedMachine_ShrinksTheHall()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 2);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var assignment = await db.DeviceSeatAssignments
                .SingleAsync(a => a.SeatId == seeded.SeatIds[1]);
            assignment.DetachedAtUtc = Now;
            await db.SaveChangesAsync();
        }

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1);

        var response = await BookAsync(client, seeded, start, end);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("no_seats_available", body!.Error);
    }

    // Полный соседний клуб — не наше дело: считается вместимость своего филиала.
    [Fact]
    public async Task AFullNeighbourClub_DoesNotCloseOurHall()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        var neighbour = await SeedAsync(factory, "4321", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, neighbour, start, end, count: 3);

        var response = await BookAsync(client, seeded, start, end);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task StartSessionAsync(
        PlatformApiFactory factory,
        Seeded seeded,
        Guid seatId,
        DateTimeOffset? endsAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var deviceId = await db.DeviceSeatAssignments
            .Where(assignment => assignment.SeatId == seatId)
            .Select(assignment => assignment.DeviceId)
            .SingleAsync();

        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(), OrganizationId = seeded.OrgId, BranchId = seeded.BranchId,
            SeatId = seatId, DeviceId = deviceId, State = SessionStateNames.Active,
            RequestedAtUtc = Now, StartedAtUtc = Now, EndsAtUtc = endsAtUtc
        });
        await db.SaveChangesAsync();
    }

    private sealed record ErrorBody(string Error);
    /// <summary>
    /// Найдено ревью: посаженную бронь исключали из подсчёта, считая, что место держит сессия. Но
    /// SeatAsync сессии не создаёт, и место переставало считаться занятым вовсе — зал с живым
    /// человеком за единственной машиной читался как пустой.
    /// </summary>
    [Fact]
    public async Task ASeatedGuestWithNoSession_StillOccupiesTheMachine()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(2);
        await SeatReservationAsync(factory, seeded, seeded.SeatIds[0], start, end);

        var response = await BookAsync(client, seeded, start.AddMinutes(30), end);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("no_seats_available", body!.Error);
    }

    // Та же посаженная бронь не должна закрывать время, до которого она не дотягивается.
    [Fact]
    public async Task ASeatedGuest_DoesNotOccupyTheMachineAfterTheirWindow()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        await SeatReservationAsync(factory, seeded, seeded.SeatIds[0], start, start.AddHours(2));

        var later = start.AddHours(3);
        var response = await BookAsync(client, seeded, later, later.AddHours(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Отменённая бронь машину не держит, даже если её успели посадить и вернуть.
    [Fact]
    public async Task ACancelledReservationNeverOccupies()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1, state: ReservationStateNames.Cancelled);

        Assert.Equal(HttpStatusCode.OK, (await BookAsync(client, seeded, start, end)).StatusCode);
    }

    // Ожидающая подтверждения бронь занимает машину так же, как подтверждённая: обещание дано.
    [Fact]
    public async Task APendingReservation_OccupiesTheMachineToo()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 1, state: ReservationStateNames.Pending);

        var response = await BookAsync(client, seeded, start, end);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Филиал без принятых игровых машин считается безлимитным намеренно: это недонастройка клуба,
    // а не ответ игроку. Ветка опасная, поэтому она должна быть под тестом, а не подразумеваться.
    [Fact]
    public async Task ABranchWithNoApprovedMachines_AcceptsBookingsInstead()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", seatCount: 2);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await foreach (var device in db.Devices.Where(d => d.BranchId == seeded.BranchId).AsAsyncEnumerable())
            {
                device.EnrollmentState = DeviceEnrollmentStateNames.Pending;
            }

            await db.SaveChangesAsync();
        }

        var start = Now.AddHours(5);
        var end = start.AddHours(1);
        await PromiseSeatsAsync(factory, seeded, start, end, count: 5);

        Assert.Equal(HttpStatusCode.OK, (await BookAsync(client, seeded, start, end)).StatusCode);
    }
}
