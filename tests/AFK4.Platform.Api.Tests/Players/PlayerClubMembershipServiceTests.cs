using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Первое действие в незнакомом клубе открывает счёт. Клуб человека не заводит — он получает
/// доступ к уже существующей личности в момент, когда она что-то у него просит.
/// </summary>
public sealed class PlayerClubMembershipServiceTests
{
    [Fact]
    public async Task FirstAction_OpensAnEmptyAccountMarkedAsComingFromTheApp()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000501");
        var club = await AddClubWithBranchAsync(factory);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.True(result.Succeeded);
        Assert.True(result.Created);
        Assert.Equal(club.OrganizationId, result.Account!.OrganizationId);
        Assert.Equal(club.BranchId, result.Account.HomeBranchId);
        Assert.Equal(person.PlatformPersonId, result.Account.PlatformPersonId);
        Assert.True(result.Account.CreatedFromApp);
        // Имя человека принадлежит человеку; клубная карточка заводится под тем же именем.
        Assert.Equal(person.DisplayName, result.Account.DisplayName);
        Assert.Equal(person.PhoneNumber, result.Account.PhoneNumber);

        // Счёт открывается нулём: ни одной записи журнала.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.LedgerEntries.AnyAsync(
            entry => entry.PlayerAccountId == result.Account.PlayerAccountId));
    }

    [Fact]
    public async Task SecondCall_ReturnsTheSameAccount_AndCreatesNothing()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000502");
        var club = await AddClubWithBranchAsync(factory);

        var first = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);
        var second = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.True(second.Succeeded);
        Assert.False(second.Created);
        Assert.Equal(first.Account!.PlayerAccountId, second.Account!.PlayerAccountId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.PlayerAccounts.CountAsync(
            account => account.OrganizationId == club.OrganizationId));
    }

    [Fact]
    public async Task CardTheOperatorMadeByHand_IsAdopted_NotDuplicated_AndKeepsItsMoney()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000503");
        var club = await AddClubWithBranchAsync(factory);
        var counterCard = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Фаррух с PS5");
        await AddTopUpAsync(factory, club, counterCard, 12_345);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.True(result.Succeeded);
        Assert.False(result.Created);
        Assert.Equal(counterCard, result.Account!.PlayerAccountId);
        // Карточку завела стойка, а не приложение: пометка происхождения не переписывается.
        Assert.False(result.Account.CreatedFromApp);
        // Клубная пометка на карточке остаётся клубной — имя человека её не затирает.
        Assert.Equal("Фаррух с PS5", result.Account.DisplayName);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.PlayerAccounts.CountAsync(
            account => account.OrganizationId == club.OrganizationId));
        Assert.Equal(12_345, await db.LedgerEntries
            .Where(entry => entry.PlayerAccountId == counterCard)
            .SumAsync(entry => entry.AmountMinorUnits));
    }

    [Fact]
    public async Task AmongSeveralUnclaimedCards_TheOneHeActuallyPlayedOn_IsAdopted()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000504");
        var club = await AddClubWithBranchAsync(factory);
        var abandoned = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Старая карточка");
        var played = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Живая карточка");
        await AddEndedSessionAsync(factory, club, played);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.Equal(played, result.Account!.PlayerAccountId);
        // Вторая карточка остаётся клубной и никуда не девается: слить две — значит двигать деньги.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var untouched = await db.PlayerAccounts.SingleAsync(account => account.PlayerAccountId == abandoned);
        Assert.Null(untouched.PlatformPersonId);
    }

    [Fact]
    public async Task SomebodyElsesCardWithTheSamePhone_IsNeverTakenOver()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000505");
        var stranger = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000506");
        var club = await AddClubWithBranchAsync(factory);
        var claimed = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Уже чужая");
        await AttachAsync(factory, claimed, stranger.PlatformPersonId);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.True(result.Created);
        Assert.NotEqual(claimed, result.Account!.PlayerAccountId);
    }

    [Fact]
    public async Task ClubWithOneBranch_NeedsNoBranchNamed()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000507");
        var club = await AddClubWithBranchAsync(factory);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, branchId: null);

        Assert.True(result.Succeeded);
        Assert.Equal(club.BranchId, result.Account!.HomeBranchId);
    }

    [Fact]
    public async Task ClubWithSeveralBranches_SaysWhichBranch_RatherThanGuessing()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000508");
        var club = await AddClubWithBranchAsync(factory);
        await AddBranchAsync(factory, club.OrganizationId, "Второй филиал");

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, branchId: null);

        Assert.False(result.Succeeded);
        Assert.Equal("branch_required", result.Error);
    }

    [Fact]
    public async Task BranchFromAnotherClub_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000509");
        var club = await AddClubWithBranchAsync(factory);
        var other = await AddClubWithBranchAsync(factory);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, other.BranchId);

        Assert.False(result.Succeeded);
        Assert.Equal("branch_not_found", result.Error);
    }

    [Fact]
    public async Task UnknownClub_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000510");

        var result = await EnsureAsync(factory, person.PlatformPersonId, Guid.NewGuid(), branchId: null);

        Assert.False(result.Succeeded);
        Assert.Equal("organization_not_found", result.Error);
    }

    /// <summary>
    /// Карточка, которую клуб закрыл, обязана оставаться закрытой. На экране деактивации обещано
    /// дословно: «Денежные операции и вход на место станут недоступны», — и первое действие из
    /// приложения не имеет права это обещание отменять.
    /// </summary>
    [Fact]
    public async Task CardTheClubClosed_IsRefused_AndNotReopened()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000511");
        var club = await AddClubWithBranchAsync(factory);
        var opened = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);
        await CloseCardAsync(factory, opened.Account!.PlayerAccountId);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.False(result.Succeeded);
        Assert.Equal("club_account_closed", result.Error);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var card = await db.PlayerAccounts.SingleAsync(
            account => account.OrganizationId == club.OrganizationId);
        Assert.False(card.IsActive);
    }

    /// <summary>
    /// Закрытая карточка, заведённая на стойке руками, — тот же запрет: она ничейная, но телефон в
    /// ней тот же самый. Завести рядом свежую значит вернуть человеку ровно то, что клуб отнял.
    /// </summary>
    [Fact]
    public async Task CounterCardTheClubClosed_IsRefused_AndNoFreshCardAppears()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000512");
        var club = await AddClubWithBranchAsync(factory);
        var counterCard = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Фаррух с PS5");
        await CloseCardAsync(factory, counterCard);

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.False(result.Succeeded);
        Assert.Equal("club_account_closed", result.Error);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.PlayerAccounts.CountAsync(
            account => account.OrganizationId == club.OrganizationId));
    }

    /// <summary>Живая ничейная карточка сильнее закрытой: подшивается та, за которой играли.</summary>
    [Fact]
    public async Task ClosedCounterCard_DoesNotBlockTheLiveOneBesideIt()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000513");
        var club = await AddClubWithBranchAsync(factory);
        var closed = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Старая карточка");
        await CloseCardAsync(factory, closed);
        var live = await AddCounterCardAsync(factory, club, person.PhoneNumber, "Фаррух с PS5");

        var result = await EnsureAsync(factory, person.PlatformPersonId, club.OrganizationId, club.BranchId);

        Assert.True(result.Succeeded);
        Assert.Equal(live, result.Account!.PlayerAccountId);
    }

    /// <summary>Закрытая карточка в одном клубе не закрывает человеку соседний.</summary>
    [Fact]
    public async Task CardClosedInOneClub_LeavesTheNeighbourClubOpen()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000514");
        var closedClub = await AddClubWithBranchAsync(factory);
        var neighbour = await AddClubWithBranchAsync(factory);
        var opened = await EnsureAsync(
            factory, person.PlatformPersonId, closedClub.OrganizationId, closedClub.BranchId);
        await CloseCardAsync(factory, opened.Account!.PlayerAccountId);

        var result = await EnsureAsync(
            factory, person.PlatformPersonId, neighbour.OrganizationId, neighbour.BranchId);

        Assert.True(result.Succeeded);
        Assert.True(result.Created);
    }

    internal sealed record SeededClub(Guid OrganizationId, Guid BranchId);

    internal static async Task<SeededClub> AddClubWithBranchAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Name = "Клуб первого действия",
            CreatedAtUtc = PlatformPersonTestData.Now,
            UpdatedAtUtc = PlatformPersonTestData.Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "central",
            Name = "Центральный",
            CreatedAtUtc = PlatformPersonTestData.Now
        });
        await db.SaveChangesAsync();
        return new SeededClub(organizationId, branchId);
    }

    internal static async Task<Guid> AddBranchAsync(
        PlatformApiFactory factory, Guid organizationId, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "second",
            Name = name,
            CreatedAtUtc = PlatformPersonTestData.Now
        });
        await db.SaveChangesAsync();
        return branchId;
    }

    /// <summary>Клуб закрыл карточку: то же самое делает оператор кнопкой «Деактивировать».</summary>
    internal static async Task CloseCardAsync(PlatformApiFactory factory, Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);
        account.IsActive = false;
        await db.SaveChangesAsync();
    }

    internal static async Task<Guid> AddCounterCardAsync(
        PlatformApiFactory factory, SeededClub club, string phoneNumber, string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerAccountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = club.OrganizationId,
            HomeBranchId = club.BranchId,
            DisplayName = displayName,
            PhoneNumber = phoneNumber,
            IsActive = true,
            CreatedAtUtc = PlatformPersonTestData.Now
        });
        await db.SaveChangesAsync();
        return playerAccountId;
    }

    private static async Task AttachAsync(PlatformApiFactory factory, Guid playerAccountId, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);
        account.PlatformPersonId = platformPersonId;
        await db.SaveChangesAsync();
    }

    private static async Task AddTopUpAsync(
        PlatformApiFactory factory, SeededClub club, Guid playerAccountId, long amountMinorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = club.OrganizationId,
            BranchId = club.BranchId,
            PlayerAccountId = playerAccountId,
            EntryType = "top_up",
            AccountType = "wallet",
            AmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS",
            Description = "Пополнение",
            Reason = "seed",
            CreatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = PlatformPersonTestData.Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task AddEndedSessionAsync(PlatformApiFactory factory, SeededClub club, Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = club.OrganizationId,
            BranchId = club.BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.Empty,
            PlayerKind = "member",
            PlayerAccountId = playerAccountId,
            TariffRuleVersionId = "v1",
            BillingMode = "prepaid_wallet",
            State = SessionStateNames.Ended,
            RequestedAtUtc = PlatformPersonTestData.Now,
            StartedAtUtc = PlatformPersonTestData.Now,
            EndedAtUtc = PlatformPersonTestData.Now.AddHours(1),
            UpdatedAtUtc = PlatformPersonTestData.Now.AddHours(1)
        });
        await db.SaveChangesAsync();
    }

    private static async Task<PlayerClubMembershipResult> EnsureAsync(
        PlatformApiFactory factory, Guid platformPersonId, Guid organizationId, Guid? branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPlayerClubMembershipService>()
            .EnsureAsync(platformPersonId, organizationId, branchId, CancellationToken.None);
    }
}
