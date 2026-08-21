using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// «Не приехал» — состояние, а не отмена с пометкой в тексте.
///
/// Пока неявку изображала отмена, три разных исхода выглядели одинаково: игрок передумал, игрок
/// не приехал и клуб отказал. Отличить их можно было только сравнением строк в свободном поле, а
/// объяснить человеку — никак. Здесь проверяется, что у неявки теперь своё состояние, свои даты и
/// своя сумма, а колонки отмены к ней не прикасаются.
/// </summary>
public sealed class ReservationNoShowStateTests
{
    private static readonly Guid OrgId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c001");
    private static readonly Guid BranchId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c002");
    private static readonly Guid ZoneId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c003");
    private static readonly Guid SeatId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c004");
    private static readonly Guid PlayerId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c005");
    private static readonly Guid TariffId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c006");
    private static readonly Guid TariffVersionId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c007");
    private static readonly Guid ShiftId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c008");
    private static readonly Guid StaffId = Guid.Parse("1e2b6a52-08f2-4a0e-9e0a-4a2f6bb3c009");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-02T18:00:00Z");

    private const long HoldAmount = 1_500;

    [Fact]
    public async Task Runner_MarksTheOutcomeAsNoShow_NotAsACancellation()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);

        await RunAsync(options, Start.AddMinutes(21));

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(
            r => r.ReservationId == booked.Response!.ReservationId);

        Assert.Equal(ReservationStateNames.NoShow, reservation.State);
        Assert.Equal(Start.AddMinutes(21), reservation.NoShowAtUtc);
        Assert.Equal(HoldAmount, reservation.RetainedAmountMinorUnits);
        // Отмена тут ни при чём: её колонки остаются пустыми, иначе исход снова станет неразличим.
        Assert.Null(reservation.CancelledAtUtc);
        Assert.Equal(string.Empty, reservation.CancelReason);
    }

    /// <summary>
    /// Филиал, который предоплату не удерживает, всё равно отмечает неявку — просто бесплатную.
    /// Сумма при этом не «ноль», а «ничего»: ноль читался бы как «удержали нисколько», хотя
    /// удержания не было вовсе.
    /// </summary>
    [Fact]
    public async Task Runner_WithoutRetention_MarksNoShowWithoutAnAmount()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: false);
        var booked = await BookAsync(options);

        await RunAsync(options, Start.AddMinutes(21));

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(
            r => r.ReservationId == booked.Response!.ReservationId);

        Assert.Equal(ReservationStateNames.NoShow, reservation.State);
        Assert.Null(reservation.RetainedAmountMinorUnits);
    }

    /// <summary>
    /// Заявка, на которую клуб не ответил, неявкой не становится ни при каких настройках — это
    /// молчание клуба, и закрывается оно отменой со своей причиной (волна 1, Ф8).
    /// </summary>
    [Fact]
    public async Task Runner_LeavesTheClubsSilenceOutOfNoShow()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true, holdSeatAfterStartMinutes: 0);
        var booked = await BookAsync(options);
        await LeaveWaitingForTheClubAsync(options, booked.Response!.ReservationId);

        await RunAsync(options, Start.AddMinutes(1));

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(
            r => r.ReservationId == booked.Response!.ReservationId);

        Assert.Equal(ReservationStateNames.Cancelled, reservation.State);
        Assert.Equal(ReservationRequestExpiryRunner.CancelReason, reservation.CancelReason);
        Assert.Null(reservation.NoShowAtUtc);
    }

    [Fact]
    public async Task Operator_CanMarkNoShow_OnABookingWhoseTimeHasCome()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);

        var result = await MarkAsync(options, booked.Response!.ReservationId, Start.AddMinutes(5));

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.NoShow, result.Response!.State);
        Assert.Equal(HoldAmount, result.Response.RetainedAmountMinorUnits);

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(
            r => r.ReservationId == booked.Response.ReservationId);
        // Отметил человек — в журнале должно быть видно, кто именно, а не безличная система.
        Assert.Equal(StaffId, reservation.UpdatedByStaffUserId);
    }

    /// <summary>
    /// Дважды удержать за одну неявку нельзя. Вторая отметка — это не ошибка стойки, а обычный
    /// повторный клик или две открытые вкладки, и отвечать на неё надо тем же результатом.
    /// </summary>
    [Fact]
    public async Task Operator_MarkingTwice_ChargesOnce()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);

        await MarkAsync(options, booked.Response!.ReservationId, Start.AddMinutes(5));
        var second = await MarkAsync(options, booked.Response.ReservationId, Start.AddMinutes(9));

        Assert.True(second.Succeeded);
        await using var db = new PlatformDbContext(options);
        Assert.Equal(1, await db.LedgerEntries.CountAsync(
            e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee));
        Assert.Equal(5_000 - HoldAmount, await WalletAsync(options));
    }

    /// <summary>Пока время брони не наступило, человек не опоздал — ему ещё ехать.</summary>
    [Fact]
    public async Task Operator_CannotMarkNoShow_BeforeTheBookingStarts()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);

        var result = await MarkAsync(options, booked.Response!.ReservationId, Start.AddMinutes(-10));

        Assert.False(result.Succeeded);
        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(
            r => r.ReservationId == booked.Response.ReservationId);
        Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
    }

    /// <summary>
    /// Заявку, которую клуб ещё не подтвердил, неявкой не отмечают: человек не обязан приезжать
    /// туда, где ему не ответили. Иначе стойка одним кликом превращала бы собственное молчание в
    /// чужую неявку — и в чужую репутацию.
    /// </summary>
    [Fact]
    public async Task Operator_CannotTurnItsOwnSilenceIntoANoShow()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);
        await LeaveWaitingForTheClubAsync(options, booked.Response!.ReservationId);

        var result = await MarkAsync(options, booked.Response.ReservationId, Start.AddMinutes(5));

        Assert.False(result.Succeeded);
    }

    /// <summary>Пришедшего и посаженного неявкой не объявляют — он сидит за ПК.</summary>
    [Fact]
    public async Task Operator_CannotMarkNoShow_OnSomeoneAlreadySeated()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);
        await SetStateAsync(options, booked.Response!.ReservationId, ReservationStateNames.Seated);

        var result = await MarkAsync(options, booked.Response.ReservationId, Start.AddMinutes(5));

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Неявка освобождает место так же, как отмена: зал не должен стоять занятым из-за того, кто
    /// не приехал.
    /// </summary>
    [Fact]
    public async Task NoShow_FreesTheSeat()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var booked = await BookAsync(options);
        await MarkAsync(options, booked.Response!.ReservationId, Start.AddMinutes(5));

        await using var db = new PlatformDbContext(options);
        var bookable = await BranchCapacity.LoadBookableSeatIdsAsync(
            db, OrgId, BranchId, CancellationToken.None);
        var busy = await BranchCapacity.CountOccupiedAsync(
            db, OrgId, BranchId, bookable, Start, Start.AddHours(1), Start.AddMinutes(5),
            CancellationToken.None);

        Assert.Equal(0, busy);
    }

    private static async Task<ReservationServiceResult<ReservationDto>> MarkAsync(
        DbContextOptions<PlatformDbContext> options, Guid reservationId, DateTimeOffset now)
    {
        await using var db = new PlatformDbContext(options);
        var clock = new FixedTimeProvider(now);
        var service = new EfReservationService(db, clock, new EfShiftService(db, clock));
        return await service.MarkNoShowAsync(
            reservationId,
            StaffId,
            new MarkReservationNoShowRequest(OrgId),
            CancellationToken.None);
    }

    private static async Task<int> RunAsync(DbContextOptions<PlatformDbContext> options, DateTimeOffset now)
    {
        await using var db = new PlatformDbContext(options);
        var clock = new FixedTimeProvider(now);
        return await new ReservationNoShowRunner(db, clock, new EfShiftService(db, clock))
            .RunOnceAsync(CancellationToken.None);
    }

    private static async Task LeaveWaitingForTheClubAsync(
        DbContextOptions<PlatformDbContext> options, Guid reservationId)
    {
        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(r => r.ReservationId == reservationId);
        reservation.State = ReservationStateNames.Pending;
        reservation.ConfirmedAtUtc = null;
        reservation.RespondByUtc = Start;
        await db.SaveChangesAsync();
    }

    private static async Task SetStateAsync(
        DbContextOptions<PlatformDbContext> options, Guid reservationId, string state)
    {
        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(r => r.ReservationId == reservationId);
        reservation.State = state;
        await db.SaveChangesAsync();
    }

    private static async Task<long> WalletAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerId, CancellationToken.None);
        return summary!.WalletBalance.MinorUnits;
    }

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task<ReservationServiceResult<ReservationDto>> BookAsync(
        DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var result = await service.CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(SeatId, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);
        Assert.True(result.Succeeded);
        return result;
    }

    private static async Task SeedAsync(
        DbContextOptions<PlatformDbContext> options,
        bool keepPrepaymentOnNoShow,
        int holdSeatAfterStartMinutes = 20)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Start });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Start });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000041", PreferredLocale = "ru", IsActive = true, CreatedAtUtc = Start
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 5_000, CurrencyCode = "TJS", CreatedAtUtc = Start.AddDays(-1)
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId, Name = "Вечерний", IsActive = true, CreatedAtUtc = Start
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId, TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1, EffectiveFromUtc = Start.AddYears(-1)
        });

        var settings = BranchBookingSettingsTestData.AcceptsAnyGuest(OrgId, BranchId, Start);
        settings.KeepPrepaymentOnNoShow = keepPrepaymentOnNoShow;
        settings.HoldSeatAfterStartMinutes = holdSeatAfterStartMinutes;
        db.BranchBookingSettings.Add(settings);

        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = ShiftId, OrganizationId = OrgId, BranchId = BranchId, OpenedByStaffUserId = StaffId,
            State = ShiftStateNames.Open, CurrencyCode = "TJS", OpenedAtUtc = Start.AddHours(-2)
        });

        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
