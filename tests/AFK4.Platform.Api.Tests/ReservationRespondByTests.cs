using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Endpoints;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Докуда клуб обещал ответить на заявку и когда ответил.
///
/// Срок ставится один раз — в момент создания заявки, из настройки филиала на эту секунду. Позже
/// клуб может передумать и поменять настройку, но у заявок, которые уже висят, обещание остаётся
/// прежним: игрок видел один обратный отсчёт и не должен обнаружить другой.
///
/// Обещать ответ позже начала брони нельзя: к этому времени отвечать уже не на что.
/// </summary>
public sealed class ReservationRespondByTests
{
    private static readonly Guid OrgId = Guid.Parse("2ad0f7a1-0001-4001-8001-000000000001");
    private static readonly Guid BranchId = Guid.Parse("2ad0f7a1-0002-4002-8002-000000000002");
    private static readonly Guid ZoneId = Guid.Parse("2ad0f7a1-0003-4003-8003-000000000003");
    private static readonly Guid SeatId = Guid.Parse("2ad0f7a1-0004-4004-8004-000000000004");
    private static readonly Guid OtherSeatId = Guid.Parse("2ad0f7a1-0005-4005-8005-000000000005");
    private static readonly Guid PlayerId = Guid.Parse("2ad0f7a1-0006-4006-8006-000000000006");
    private static readonly Guid TariffId = Guid.Parse("2ad0f7a1-0007-4007-8007-000000000007");
    private static readonly Guid TariffVersionId = Guid.Parse("2ad0f7a1-0008-4008-8008-000000000008");
    private static readonly Guid StaffId = Guid.Parse("2ad0f7a1-0009-4009-8009-000000000009");

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");
    private static readonly DateTimeOffset Start = Now.AddHours(4);

    [Fact]
    public async Task PendingRequest_PromisesAnAnswerWithinTheBranchSetting()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));

        var booked = await BookAsync(options);

        Assert.Equal(ReservationStateNames.Pending, booked.Response!.State);
        Assert.Equal(Now.AddMinutes(30), booked.Response.RespondByUtc);
        Assert.Null(booked.Response.ConfirmedAtUtc);
    }

    /// <summary>
    /// Бронь через двадцать минут, а обещание — полчаса: отвечать после начала уже некому,
    /// поэтому срок упирается в начало брони.
    /// </summary>
    [Fact]
    public async Task PendingRequest_NeverPromisesAnAnswerAfterTheBookingStarts()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));

        var soon = Now.AddMinutes(20);
        var booked = await BookAsync(options, startsAtUtc: soon);

        Assert.Equal(soon, booked.Response!.RespondByUtc);
    }

    [Fact]
    public async Task AutoConfirmedBooking_HasNoDeadlineAndCarriesTheAnswerTime()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Auto));

        var booked = await BookAsync(options);

        Assert.Equal(ReservationStateNames.Confirmed, booked.Response!.State);
        Assert.Null(booked.Response.RespondByUtc);
        Assert.Equal(Now, booked.Response.ConfirmedAtUtc);
    }

    [Fact]
    public async Task Confirming_StampsWhenTheClubAnswered()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));
        var booked = await BookAsync(options);

        var answeredAt = Now.AddMinutes(7);
        await using var db = new PlatformDbContext(options);
        var confirmed = await new EfReservationService(db, new FixedTimeProvider(answeredAt)).ConfirmAsync(
            booked.Response!.ReservationId,
            StaffId,
            new ConfirmReservationRequest(OrgId, booked.Response.Version),
            CancellationToken.None);

        Assert.True(confirmed.Succeeded);
        Assert.Equal(ReservationStateNames.Confirmed, confirmed.Response!.State);
        Assert.Equal(answeredAt, confirmed.Response.ConfirmedAtUtc);
        // Срок остаётся в карточке как след обещания: он больше ничего не решает, но по нему
        // видно, уложился клуб в своё слово или нет.
        Assert.Equal(Now.AddMinutes(30), confirmed.Response.RespondByUtc);
    }

    /// <summary>
    /// Человека усадили за машину, минуя «подтвердить». Это тоже ответ клуба, и он отмечается:
    /// иначе посаженная заявка выглядела бы так, будто ей никто и не ответил.
    /// </summary>
    [Fact]
    public async Task Seating_CountsAsTheAnswerToo()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));
        var booked = await BookAsync(options);

        var seatedAt = Now.AddMinutes(3);
        await using var db = new PlatformDbContext(options);
        var seated = await new EfReservationService(db, new FixedTimeProvider(seatedAt)).SeatAsync(
            booked.Response!.ReservationId,
            StaffId,
            new SeatReservationRequest(OrgId, booked.Response.Version),
            CancellationToken.None);

        Assert.True(seated.Succeeded);
        Assert.Equal(ReservationStateNames.Seated, seated.Response!.State);
        Assert.Equal(seatedAt, seated.Response.ConfirmedAtUtc);
    }

    /// <summary>
    /// Клуб передумал и попросил себе два часа — но у заявки, которая уже висит, обещание
    /// прежнее. Иначе игрок увидел бы, как обратный отсчёт вдруг поехал вперёд.
    /// </summary>
    [Fact]
    public async Task ChangingTheSettingLater_DoesNotMoveAlreadyPendingRequests()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));
        var booked = await BookAsync(options);

        await using (var settingsDb = new PlatformDbContext(options))
        {
            var settings = await settingsDb.BranchBookingSettings.SingleAsync(row => row.BranchId == BranchId);
            settings.RespondWithinMinutes = 120;
            await settingsDb.SaveChangesAsync();
        }

        await using var db = new PlatformDbContext(options);
        var stored = await db.Reservations.SingleAsync(row => row.ReservationId == booked.Response!.ReservationId);
        Assert.Equal(Now.AddMinutes(30), stored.RespondByUtc);
    }

    /// <summary>
    /// Заявка, заведённая на стойке, тоже ждёт ответа клуба — и тоже под сроком. Иначе брони из
    /// приложения жили бы по часам, а заведённые руками висели бы вечно.
    /// </summary>
    [Fact]
    public async Task OperatorCreatedRequest_GetsTheSameDeadline()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 45));

        await using var db = new PlatformDbContext(options);
        var created = await new EfReservationService(db, new FixedTimeProvider(Now)).CreateAsync(
            BranchId,
            StaffId,
            new CreateReservationRequest(
                OrgId,
                PlayerId,
                OtherSeatId,
                CustomerName: "Игрок",
                PhoneNumber: null,
                StartsAtUtc: Start,
                DurationMinutes: 60,
                Source: ReservationSourceNames.Online,
                Note: null),
            CancellationToken.None);

        Assert.True(created.Succeeded);
        Assert.Equal(ReservationStateNames.Pending, created.Response!.State);
        Assert.Equal(Now.AddMinutes(45), created.Response.RespondByUtc);
    }

    /// <summary>
    /// Бронь передвинули на более раннее время — обещание ответа едет вместе с ней: отвечать
    /// после начала уже некому.
    /// </summary>
    [Fact]
    public async Task MovingTheBookingEarlier_PullsTheDeadlineIn()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 60));
        var booked = await BookAsync(options);

        var earlier = Now.AddMinutes(10);
        await using var db = new PlatformDbContext(options);
        var updated = await new EfReservationService(db, new FixedTimeProvider(Now)).UpdateAsync(
            booked.Response!.ReservationId,
            StaffId,
            new UpdateReservationRequest(
                OrgId,
                PlayerAccountId: null,
                SeatId: null,
                CustomerName: null,
                PhoneNumber: null,
                StartsAtUtc: earlier,
                DurationMinutes: null,
                Source: null,
                Note: null,
                ExpectedVersion: booked.Response.Version),
            CancellationToken.None);

        Assert.True(updated.Succeeded);
        Assert.Equal(earlier, updated.Response!.RespondByUtc);
    }

    /// <summary>
    /// Обещание одно на всю компанию: у каждого места группы тот же срок, что и у остальных.
    /// </summary>
    [Fact]
    public async Task GroupRequest_PromisesOneAnswerForTheWholeCompany()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));

        await using var db = new PlatformDbContext(options);
        var group = await new EfReservationService(db, new FixedTimeProvider(Now)).CreateOnlineGroupAsync(
            PlayerId,
            OrgId,
            BranchId,
            new CreatePlayerReservationGroupRequest(2, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);

        Assert.True(group.Succeeded);
        Assert.All(group.Response!, reservation => Assert.Equal(Now.AddMinutes(30), reservation.RespondByUtc));
    }

    /// <summary>
    /// Срок доезжает и до стойки, и до приложения. Иначе у двух администраторов на разных машинах
    /// были бы разные цифры, а у игрока третья.
    /// </summary>
    [Fact]
    public async Task Deadline_ReachesThePlayerToo()
    {
        var options = NewOptions();
        await SeedAsync(options, Settings(BranchBookingAcceptanceModes.Manual, respondWithinMinutes: 30));
        var booked = await BookAsync(options);

        var playerView = EndpointHelpers.ToPlayerReservationDto(booked.Response!);

        Assert.Equal(Now.AddMinutes(30), playerView.RespondByUtc);
    }

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task<ReservationServiceResult<ReservationDto>> BookAsync(
        DbContextOptions<PlatformDbContext> options,
        DateTimeOffset? startsAtUtc = null)
    {
        await using var db = new PlatformDbContext(options);
        var start = startsAtUtc ?? Start;
        var result = await new EfReservationService(db, new FixedTimeProvider(Now)).CreateOnlineAsync(
            PlayerId,
            OrgId,
            BranchId,
            new CreatePlayerReservationRequest(SeatId, start, start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);
        Assert.True(result.Succeeded);
        return result;
    }

    private static BranchBookingSettingsEntity Settings(
        string acceptanceMode,
        int respondWithinMinutes = BranchBookingSettingsDefaults.RespondWithinMinutes) => new()
        {
            BranchId = BranchId,
            OrganizationId = OrgId,
            AcceptanceMode = acceptanceMode,
            RespondWithinMinutes = respondWithinMinutes,
            RequirePrepaymentFromNewGuests = false,
            MaxActiveReservationsForNewGuests = BranchBookingSettingsDefaults.MaxActiveReservationsLimit,
            RegularAfterVisits = BranchBookingSettingsDefaults.RegularAfterVisits,
            HoldSeatAfterStartMinutes = BranchBookingSettingsDefaults.HoldSeatAfterStartMinutes,
            KeepPrepaymentOnNoShow = BranchBookingSettingsDefaults.KeepPrepaymentOnNoShow,
            UpdatedAtUtc = Now,
            UpdatedByStaffUserId = StaffId
        };

    private static async Task SeedAsync(
        DbContextOptions<PlatformDbContext> options,
        BranchBookingSettingsEntity settings)
    {
        await using var db = new PlatformDbContext(options);
        db.Organizations.Add(new OrganizationEntity { OrganizationId = OrgId, Name = "Клуб", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId, OrganizationId = OrgId, Slug = "branch-01", Name = "Филиал",
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId,
            Name = "ПК-01", SortOrder = 10, CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = OtherSeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId,
            Name = "ПК-02", SortOrder = 20, CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000001", PreferredLocale = "ru", MarketingOptIn = false, IsActive = true,
            CreatedAtUtc = Now
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId, Name = "Дневной", IsActive = true,
            CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId, TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 10,
            MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1, EffectiveFromUtc = Now.AddYears(-1)
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 100_000, CurrencyCode = "TJS", CreatedAtUtc = Now.AddDays(-1)
        });
        db.BranchBookingSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
