using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tournaments;
using AFK4.Shared.Contracts.Billing;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Tournaments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Tournaments;

/// <summary>
/// События клуба: турнир по пятницам, ночь игры, чемпионат зала. Клуб заполняет ими мёртвые дни,
/// поэтому здесь ходят деньги — взнос за участие, — и главные проверки про них.
/// </summary>
public sealed class EfTournamentServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Friday = new(2026, 8, 28, 19, 0, 0, TimeSpan.Zero);

    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static EfTournamentService NewService(PlatformDbContext db, MovableTimeProvider? clock = null) =>
        new(db, clock ?? new MovableTimeProvider(Now));

    private static async Task SeedBranchAsync(PlatformDbContext db)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = Branch,
            OrganizationId = Org,
            Slug = "main",
            Name = "На Рудаки",
            City = "Душанбе",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    /// Игрок с деньгами на клубном кошельке: взнос списывается именно оттуда.
    private static async Task<Guid> SeedPlayerAsync(PlatformDbContext db, long walletMinorUnits)
    {
        var playerAccountId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = Org,
            HomeBranchId = Branch,
            DisplayName = "Фаррух",
            PhoneNumber = TestPhones.Next(),
            PreferredLocale = "ru",
            IsActive = true,
            CreatedAtUtc = Now
        });

        if (walletMinorUnits != 0)
        {
            db.LedgerEntries.Add(BillingEntryFactory.Create(
                Org, Branch, playerAccountId, null, null,
                LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
                walletMinorUnits, 0, "TJS", "top up", "seed", null, Staff, Now));
        }

        await db.SaveChangesAsync();
        return playerAccountId;
    }

    private static CreateTournamentRequest ValidCreate(long entryFee = 2000, int capacity = 2) =>
        new(Branch, "Ночь Counter-Strike", "Пять на пять, свои команды", "Counter-Strike",
            Friday, entryFee, capacity);

    private static async Task<Guid> PublishedTournamentAsync(
        PlatformDbContext db, EfTournamentService service, long entryFee = 2000, int capacity = 2)
    {
        var created = await service.CreateAsync(Org, Staff, ValidCreate(entryFee, capacity), CancellationToken.None);
        await service.PublishAsync(Org, created.Value!.TournamentId, CancellationToken.None);
        return created.Value.TournamentId;
    }

    private static async Task<long> WalletAsync(PlatformDbContext db, Guid playerAccountId)
    {
        var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(db, playerAccountId, CancellationToken.None);
        return wallet?.WalletBalance.MinorUnits ?? 0;
    }

    // Событие заводится черновиком: пока клуб дописывает условия, игроку его видеть рано.
    [Fact]
    public async Task Create_LeavesTheEventADraftUntilTheClubPublishesIt()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);

        var created = await service.CreateAsync(Org, Staff, ValidCreate(), CancellationToken.None);

        Assert.True(created.Succeeded);
        Assert.Equal(TournamentStateNames.Draft, created.Value!.State);
        Assert.Equal("TJS", created.Value.EntryFee.CurrencyCode);
        Assert.Empty(await service.ListForPlayerAsync(Org, Branch, player, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_MakesTheEventVisibleToPlayers()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        await PublishedTournamentAsync(db, service);

        var visible = Assert.Single(await service.ListForPlayerAsync(Org, Branch, player, CancellationToken.None));
        Assert.Equal("Ночь Counter-Strike", visible.Title);
        Assert.Equal(2000, visible.EntryFee.MinorUnits);
        Assert.False(visible.IsRegistered);
        Assert.Equal("На Рудаки", visible.BranchName);
    }

    // Взнос — настоящие деньги клубного кошелька, а не пометка «записан».
    [Fact]
    public async Task Register_ChargesTheEntryFeeFromTheWallet()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);

        var registered = await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.True(registered.Succeeded);
        Assert.True(registered.Value!.IsRegistered);
        Assert.Equal(1, registered.Value.RegisteredCount);
        Assert.Equal(8000, await WalletAsync(db, player));
    }

    [Fact]
    public async Task Register_WithoutEnoughMoney_RefusesAndChargesNothing()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 500);
        var tournamentId = await PublishedTournamentAsync(db, service);

        var registered = await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.False(registered.Succeeded);
        Assert.Equal(TournamentRefusalCodes.InsufficientFunds, registered.Error);
        Assert.Equal(500, await WalletAsync(db, player));
        Assert.Empty(await db.TournamentRegistrations.ToListAsync());
    }

    // Бесплатный вечер — обычный случай: клуб просто заполняет будний день.
    [Fact]
    public async Task Register_ForAFreeEvent_TouchesNoMoney()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 0);
        var tournamentId = await PublishedTournamentAsync(db, service, entryFee: 0);

        var registered = await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.True(registered.Succeeded);
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.PlayerAccountId == player).ToListAsync());
        Assert.Equal(0, await WalletAsync(db, player));
    }

    [Fact]
    public async Task Register_WhenTheEventIsFull_Refuses()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var first = await SeedPlayerAsync(db, 10_000);
        var second = await SeedPlayerAsync(db, 10_000);
        var third = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service, capacity: 2);

        await service.RegisterAsync(Org, first, tournamentId, CancellationToken.None);
        await service.RegisterAsync(Org, second, tournamentId, CancellationToken.None);
        var overflow = await service.RegisterAsync(Org, third, tournamentId, CancellationToken.None);

        Assert.Equal(TournamentRefusalCodes.Full, overflow.Error);
        Assert.Equal(10_000, await WalletAsync(db, third));
    }

    // Два нажатия «Записаться» не должны списать взнос дважды.
    [Fact]
    public async Task Register_Twice_ChargesOnce()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);

        await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);
        var again = await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.Equal(TournamentRefusalCodes.AlreadyRegistered, again.Error);
        Assert.Equal(8000, await WalletAsync(db, player));
    }

    [Fact]
    public async Task Register_ForADraft_Refuses()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        var created = await service.CreateAsync(Org, Staff, ValidCreate(), CancellationToken.None);

        var registered = await service.RegisterAsync(Org, player, created.Value!.TournamentId, CancellationToken.None);

        Assert.Equal(TournamentRefusalCodes.NotPublished, registered.Error);
    }

    // Событие началось — записываться поздно, и это не «мест нет», а другая новость.
    [Fact]
    public async Task Register_AfterTheEventStarted_Refuses()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var clock = new MovableTimeProvider(Now);
        var service = NewService(db, clock);
        var player = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);

        clock.Advance(Friday - Now + TimeSpan.FromMinutes(1));
        var registered = await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.Equal(TournamentRefusalCodes.AlreadyStarted, registered.Error);
        Assert.Equal(10_000, await WalletAsync(db, player));
    }

    // Снялся заранее — место освободилось, клуб успевает продать его другому, деньги назад.
    [Fact]
    public async Task CancelRegistration_BeforeTheStart_ReturnsTheFee()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);
        await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        var cancelled = await service.CancelRegistrationAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.True(cancelled.Succeeded);
        Assert.False(cancelled.Value!.IsRegistered);
        Assert.Equal(0, cancelled.Value.RegisteredCount);
        Assert.Equal(10_000, await WalletAsync(db, player));
    }

    // Снялся — и записался снова: отменённая запись не должна закрывать дорогу обратно.
    [Fact]
    public async Task Register_AfterCancelling_WorksAgain()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);
        await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);
        await service.CancelRegistrationAsync(Org, player, tournamentId, CancellationToken.None);

        var again = await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        Assert.True(again.Succeeded);
        Assert.Equal(8000, await WalletAsync(db, player));
    }

    // Клуб отменил вечер — это не решение игрока, и удерживать с него нечего.
    [Fact]
    public async Task ClubCancels_ReturnsTheFeeToEveryone()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var first = await SeedPlayerAsync(db, 10_000);
        var second = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);
        await service.RegisterAsync(Org, first, tournamentId, CancellationToken.None);
        await service.RegisterAsync(Org, second, tournamentId, CancellationToken.None);

        var cancelled = await service.CancelAsync(Org, tournamentId, Staff, "Свет отключили", CancellationToken.None);

        Assert.True(cancelled.Succeeded);
        Assert.Equal(TournamentStateNames.Cancelled, cancelled.Value!.State);
        Assert.Equal("Свет отключили", cancelled.Value.CancelReason);
        Assert.Equal(10_000, await WalletAsync(db, first));
        Assert.Equal(10_000, await WalletAsync(db, second));
    }

    // Отменённое клубом событие обязано доехать до того, кто на него шёл, — вместе с причиной.
    [Fact]
    public async Task ClubCancels_TheEventStaysVisibleToThoseWhoWereGoing()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var going = await SeedPlayerAsync(db, 10_000);
        var stranger = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);
        await service.RegisterAsync(Org, going, tournamentId, CancellationToken.None);

        await service.CancelAsync(Org, tournamentId, Staff, "Свет отключили", CancellationToken.None);

        var forGoing = Assert.Single(await service.ListForPlayerAsync(Org, Branch, going, CancellationToken.None));
        Assert.Equal(TournamentStateNames.Cancelled, forGoing.State);
        Assert.Equal("Свет отключили", forGoing.CancelReason);
        Assert.Empty(await service.ListForPlayerAsync(Org, Branch, stranger, CancellationToken.None));
    }

    // Опустить потолок ниже числа записавшихся значит выгнать кого-то задним числом.
    [Fact]
    public async Task Update_CannotShrinkCapacityBelowTheRegistered()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var first = await SeedPlayerAsync(db, 10_000);
        var second = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service, capacity: 4);
        await service.RegisterAsync(Org, first, tournamentId, CancellationToken.None);
        await service.RegisterAsync(Org, second, tournamentId, CancellationToken.None);

        var shrunk = await service.UpdateAsync(
            Org, tournamentId, new UpdateTournamentRequest(Capacity: 1), CancellationToken.None);

        Assert.Equal(TournamentRefusalCodes.Full, shrunk.Error);
    }

    [Fact]
    public async Task Participants_ListWhoIsComing()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var player = await SeedPlayerAsync(db, 10_000);
        var tournamentId = await PublishedTournamentAsync(db, service);
        await service.RegisterAsync(Org, player, tournamentId, CancellationToken.None);

        var participants = await service.ListParticipantsAsync(Org, tournamentId, CancellationToken.None);

        var single = Assert.Single(participants.Value!);
        Assert.Equal("Фаррух", single.DisplayName);
        Assert.Equal(2000, single.EntryFeePaid.MinorUnits);
    }

    // Чужое событие не должно ни читаться, ни правиться по прямой ссылке.
    [Fact]
    public async Task AnotherClub_CannotTouchTheEvent()
    {
        await using var db = NewDb();
        await SeedBranchAsync(db);
        var service = NewService(db);
        var tournamentId = await PublishedTournamentAsync(db, service);

        var stranger = Guid.NewGuid();
        Assert.True((await service.UpdateAsync(
            stranger, tournamentId, new UpdateTournamentRequest(Title: "Чужое"), CancellationToken.None)).NotFound);
        Assert.True((await service.CancelAsync(
            stranger, tournamentId, Staff, "нет", CancellationToken.None)).NotFound);
        Assert.True((await service.ListParticipantsAsync(
            stranger, tournamentId, CancellationToken.None)).NotFound);
    }
}
