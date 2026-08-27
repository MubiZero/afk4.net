using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Friends;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Friends;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Friends;

/// <summary>
/// Друзья и «я сейчас в зале».
///
/// Главное здесь не список, а приватность: присутствие видят только принятые друзья, ответ на
/// заявку по чужому номеру ничего не рассказывает о том, есть ли этот номер в сети, и человек
/// одним переключателем становится невидимым.
/// </summary>
public sealed class EfFriendServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 20, 0, 0, TimeSpan.Zero);

    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static EfFriendService NewService(PlatformDbContext db) =>
        new(db, new MovableTimeProvider(Now));

    private static async Task<(Guid PersonId, string Phone)> SeedPersonAsync(
        PlatformDbContext db, string name, bool showsPresence = true)
    {
        var personId = Guid.NewGuid();
        var phone = TestPhones.Next();
        db.PlatformPersons.Add(new PlatformPersonEntity
        {
            PlatformPersonId = personId,
            PhoneNumber = phone,
            DisplayName = name,
            ShowsPresenceToFriends = showsPresence,
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (personId, phone);
    }

    /// Человек сел за ПК в клубе: клубная карточка, филиал и живая сессия.
    private static async Task SeatAsync(PlatformDbContext db, Guid personId, string club, string hall)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = club,
            Status = "active",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "hall",
            Name = hall,
            City = "Душанбе",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = accountId,
            OrganizationId = organizationId,
            HomeBranchId = branchId,
            PlatformPersonId = personId,
            DisplayName = "карточка",
            PhoneNumber = "+992000000000",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerAccountId = accountId,
            State = SessionStateNames.Active,
            RequestedAtUtc = Now,
            StartedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> BecomeFriendsAsync(
        PlatformDbContext db, EfFriendService service, Guid first, Guid second, string secondPhone)
    {
        await service.RequestAsync(first, secondPhone, CancellationToken.None);
        var incoming = await service.ListAsync(second, CancellationToken.None);
        var request = Assert.Single(incoming.Incoming);
        await service.AcceptAsync(second, request.FriendRequestId, CancellationToken.None);
        return request.FriendRequestId;
    }

    [Fact]
    public async Task Request_ThenAccept_MakesThemFriendsOnBothSides()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");

        await BecomeFriendsAsync(db, service, me, friend, friendPhone);

        var mine = await service.ListAsync(me, CancellationToken.None);
        var theirs = await service.ListAsync(friend, CancellationToken.None);
        Assert.Equal("Далер", Assert.Single(mine.Friends).DisplayName);
        Assert.Equal("Фаррух", Assert.Single(theirs.Friends).DisplayName);
        Assert.Empty(mine.Incoming);
        Assert.Empty(mine.Outgoing);
    }

    // Заявка ждёт ответа у того, кому она пришла, и висит «отправленной» у того, кто позвал.
    [Fact]
    public async Task Request_ShowsUpOnBothSidesWhileItWaits()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");

        await service.RequestAsync(me, friendPhone, CancellationToken.None);

        Assert.Equal("Далер", Assert.Single((await service.ListAsync(me, CancellationToken.None)).Outgoing).DisplayName);
        Assert.Equal("Фаррух", Assert.Single((await service.ListAsync(friend, CancellationToken.None)).Incoming).DisplayName);
    }

    /// Ответ на заявку по чужому номеру одинаков всегда — иначе приложение стало бы способом
    /// проверять, зарегистрирован ли номер в сети.
    [Fact]
    public async Task Request_ToAnUnknownNumber_AnswersExactlyLikeARealOne()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (_, realPhone) = await SeedPersonAsync(db, "Далер");

        var toReal = await service.RequestAsync(me, realPhone, CancellationToken.None);
        var toNobody = await service.RequestAsync(me, "+992559999999", CancellationToken.None);

        Assert.Equal(toReal.Succeeded, toNobody.Succeeded);
        Assert.Equal(toReal.Error, toNobody.Error);
    }

    // Отказавшего второй раз не зовут — но и знать об отказе позвавший не должен.
    [Fact]
    public async Task Request_AfterADecline_AnswersTheSameAndCreatesNothingNew()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await service.RequestAsync(me, friendPhone, CancellationToken.None);
        var incoming = Assert.Single((await service.ListAsync(friend, CancellationToken.None)).Incoming);
        await service.DeclineAsync(friend, incoming.FriendRequestId, CancellationToken.None);

        var again = await service.RequestAsync(me, friendPhone, CancellationToken.None);

        Assert.True(again.Succeeded);
        Assert.Empty((await service.ListAsync(friend, CancellationToken.None)).Incoming);
        Assert.Single(await db.PersonFriendships.ToListAsync());
    }

    [Fact]
    public async Task Request_ToMyOwnNumber_IsRefusedOutLoud()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, myPhone) = await SeedPersonAsync(db, "Фаррух");

        var result = await service.RequestAsync(me, myPhone, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(FriendRefusalCodes.Self, result.Error);
    }

    // Ради этого список и открывают: видно, кто сейчас в зале и в каком.
    [Fact]
    public async Task Friend_AtAClub_IsShownWithTheClubAndTheHall()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await BecomeFriendsAsync(db, service, me, friend, friendPhone);
        await SeatAsync(db, friend, "CyberX", "На Рудаки");

        var mine = await service.ListAsync(me, CancellationToken.None);

        var presence = Assert.Single(mine.Friends).Presence;
        Assert.NotNull(presence);
        Assert.Equal("CyberX", presence!.OrganizationName);
        Assert.Equal("На Рудаки", presence.BranchName);
    }

    /// Присутствие — не публичное поле: посторонний не видит его, даже если знает, кого искать.
    [Fact]
    public async Task Presence_IsInvisibleToEveryoneWhoIsNotAFriend()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (stranger, _) = await SeedPersonAsync(db, "Незнакомец");
        var (player, _) = await SeedPersonAsync(db, "Далер");
        await SeatAsync(db, player, "CyberX", "На Рудаки");

        var mine = await service.ListAsync(stranger, CancellationToken.None);

        Assert.Empty(mine.Friends);
    }

    // Пока заявка не принята, друзьями они не стали — и в зале друг друга не видят.
    [Fact]
    public async Task Presence_IsInvisibleWhileTheRequestIsStillWaiting()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await service.RequestAsync(me, friendPhone, CancellationToken.None);
        await SeatAsync(db, friend, "CyberX", "На Рудаки");

        var mine = await service.ListAsync(me, CancellationToken.None);

        Assert.Empty(mine.Friends);
        Assert.Single(mine.Outgoing);
    }

    // Один переключатель — и человека не видно нигде, хотя друзья остаются друзьями.
    [Fact]
    public async Task PresenceSwitch_HidesTheHallFromFriendsButKeepsThem()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await BecomeFriendsAsync(db, service, me, friend, friendPhone);
        await SeatAsync(db, friend, "CyberX", "На Рудаки");

        await service.SetPresenceVisibilityAsync(friend, showsPresence: false, CancellationToken.None);
        var mine = await service.ListAsync(me, CancellationToken.None);

        var single = Assert.Single(mine.Friends);
        Assert.Equal("Далер", single.DisplayName);
        Assert.Null(single.Presence);
    }

    [Fact]
    public async Task PresenceSwitch_IsReportedBackToItsOwner()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");

        Assert.True((await service.ListAsync(me, CancellationToken.None)).ShowsPresence);
        await service.SetPresenceVisibilityAsync(me, showsPresence: false, CancellationToken.None);
        Assert.False((await service.ListAsync(me, CancellationToken.None)).ShowsPresence);
    }

    /// Принять чужую заявку за другого нельзя — даже свою отправленную.
    [Fact]
    public async Task Accept_OfSomeoneElsesRequest_IsRefused()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await service.RequestAsync(me, friendPhone, CancellationToken.None);
        var outgoing = Assert.Single((await service.ListAsync(me, CancellationToken.None)).Outgoing);

        var result = await service.AcceptAsync(me, outgoing.FriendRequestId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(FriendRefusalCodes.NoSuchRequest, result.Error);
    }

    // Расстались — не отказ: позвать друг друга снова должно быть можно.
    [Fact]
    public async Task Remove_LetsThemBecomeFriendsAgain()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await BecomeFriendsAsync(db, service, me, friend, friendPhone);

        await service.RemoveAsync(me, friend, CancellationToken.None);
        Assert.Empty((await service.ListAsync(me, CancellationToken.None)).Friends);
        Assert.Empty((await service.ListAsync(friend, CancellationToken.None)).Friends);

        await service.RequestAsync(me, friendPhone, CancellationToken.None);
        Assert.Single((await service.ListAsync(friend, CancellationToken.None)).Incoming);
    }

    // Кто в зале — первым: ради этого список и открывают.
    [Fact]
    public async Task Friends_WhoAreAtAClubComeFirst()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (away, awayPhone) = await SeedPersonAsync(db, "Абдулло");
        var (playing, playingPhone) = await SeedPersonAsync(db, "Ясин");
        await BecomeFriendsAsync(db, service, me, away, awayPhone);
        await BecomeFriendsAsync(db, service, me, playing, playingPhone);
        await SeatAsync(db, playing, "CyberX", "На Рудаки");

        var mine = await service.ListAsync(me, CancellationToken.None);

        Assert.Equal(["Ясин", "Абдулло"], mine.Friends.Select(friend => friend.DisplayName).ToArray());
    }

    /// Дружба не даёт доступа к чужому счёту: в ответе нет ни телефона, ни денег.
    [Fact]
    public async Task Friend_CarriesNothingButANameAndAHall()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var (me, _) = await SeedPersonAsync(db, "Фаррух");
        var (friend, friendPhone) = await SeedPersonAsync(db, "Далер");
        await BecomeFriendsAsync(db, service, me, friend, friendPhone);

        var single = Assert.Single((await service.ListAsync(me, CancellationToken.None)).Friends);

        var properties = single.GetType().GetProperties().Select(property => property.Name).ToArray();
        Assert.Equal(["PlatformPersonId", "DisplayName", "Presence"], properties);
    }
}
