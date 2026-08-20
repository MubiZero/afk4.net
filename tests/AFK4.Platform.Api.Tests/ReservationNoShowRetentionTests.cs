using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Что происходит с предоплатой, когда игрок не приехал.
///
/// Решение владельца: удержанная предоплата — это выручка клуба, а не «деньги просто не
/// вернулись». Поэтому в журнале появляются две записи, а не ноль: заморозка снимается (реверс) и
/// тут же выписывается удержание. Кошелёк в итоге там же, где был при живой брони, но объяснить
/// каждый тийин теперь можно, и в кассовой смене удержание видно отдельной строкой.
///
/// Держать место дольше или меньше — решение филиала (<c>HoldSeatAfterStartMinutes</c>), а не
/// зашитая в код четверть часа.
/// </summary>
public sealed class ReservationNoShowRetentionTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
    private static readonly Guid TariffId = Guid.Parse("11111111-1111-4111-1111-111111111111");
    private static readonly Guid TariffVersionId = Guid.Parse("22222222-2222-4222-2222-222222222222");
    private static readonly Guid ShiftId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid StaffId = Guid.Parse("99999999-9999-4999-9999-999999999999");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-18T16:00:00Z");

    /// <summary>Час по 25 за минуту — 1500 замороженных единиц под каждую бронь.</summary>
    private const long HoldAmount = 1_500;

    [Fact]
    public async Task NoShow_WithoutRetention_ReturnsTheMoneyAndWritesNoFee()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: false, openShift: true);
        await BookAsync(options);

        await RunAsync(options, Start.AddMinutes(21));

        Assert.Equal(5_000, await WalletAsync(options));
        await using var db = new PlatformDbContext(options);
        Assert.False(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee));
    }

    [Fact]
    public async Task NoShow_WithRetention_KeepsTheMoneyAsClubRevenue()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true, openShift: true);
        await BookAsync(options);

        Assert.Equal(1, await RunAsync(options, Start.AddMinutes(21)));

        // Кошелёк там же, где был при живой брони: заморозка снята, удержание выписано.
        Assert.Equal(5_000 - HoldAmount, await WalletAsync(options));

        await using var db = new PlatformDbContext(options);
        var release = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Reversal);
        var fee = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee);

        Assert.Equal(HoldAmount, release.AmountMinorUnits);
        Assert.Equal(-HoldAmount, fee.AmountMinorUnits);
        Assert.Equal(LedgerAccountTypeNames.Wallet, fee.AccountType);
        Assert.Equal("TJS", fee.CurrencyCode);
        // Проводка в кассовую смену: удержание отвечает та смена, при которой оно случилось.
        Assert.Equal(ShiftId, fee.ShiftId);
    }

    /// <summary>Заморозка снята — значит и «придержано» обнулилось: денег больше не придерживают.</summary>
    [Fact]
    public async Task NoShow_WithRetention_LeavesNothingHeld()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true, openShift: true);
        await BookAsync(options);

        await RunAsync(options, Start.AddMinutes(21));

        await using var db = new PlatformDbContext(options);
        var balances = await LedgerBalanceProjector.GetClubBalancesAsync(db, PlayerId, CancellationToken.None);
        Assert.Equal(0, balances.HeldMinorUnits);
        Assert.Equal(5_000 - HoldAmount, balances.WalletMinorUnits);
    }

    /// <summary>
    /// Ночью клуб закрыт, смены нет — удержание всё равно выписывается, но без смены: ни одна
    /// смена за него не отвечала. Держать деньги замороженными до открытия было бы хуже для игрока.
    /// </summary>
    [Fact]
    public async Task NoShow_WithRetention_OutsideAnOpenShift_WritesTheFeeWithoutAShift()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true, openShift: false);
        await BookAsync(options);

        Assert.Equal(1, await RunAsync(options, Start.AddMinutes(21)));

        await using var db = new PlatformDbContext(options);
        var fee = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee);
        Assert.Null(fee.ShiftId);
        Assert.Equal(5_000 - HoldAmount, await WalletAsync(options));
    }

    [Fact]
    public async Task NoShow_WithRetention_SecondRunChargesNothingTwice()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true, openShift: true);
        await BookAsync(options);

        await RunAsync(options, Start.AddMinutes(21));
        Assert.Equal(0, await RunAsync(options, Start.AddMinutes(40)));

        await using var db = new PlatformDbContext(options);
        Assert.Equal(1, await db.LedgerEntries.CountAsync(e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee));
        Assert.Equal(5_000 - HoldAmount, await WalletAsync(options));
    }

    /// <summary>
    /// Игрока ждут столько, сколько решил филиал. Сорок минут — значит на двадцать первой минуте
    /// бронь ещё жива, а зашитая в код четверть часа больше ничего не решает.
    /// </summary>
    [Fact]
    public async Task NoShow_WaitsAsLongAsTheBranchAsked()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: false, openShift: true, holdSeatAfterStartMinutes: 40);
        await BookAsync(options);

        Assert.Equal(0, await RunAsync(options, Start.AddMinutes(21)));
        Assert.Equal(3_500, await WalletAsync(options));

        Assert.Equal(1, await RunAsync(options, Start.AddMinutes(41)));
        Assert.Equal(5_000, await WalletAsync(options));
    }

    /// <summary>Филиал, который место не держит вовсе: неявка разбирается сразу после начала.</summary>
    [Fact]
    public async Task NoShow_WithZeroHoldMinutes_ResolvesRightAfterTheStart()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: false, openShift: true, holdSeatAfterStartMinutes: 0);
        await BookAsync(options);

        Assert.Equal(1, await RunAsync(options, Start.AddMinutes(1)));
        Assert.Equal(5_000, await WalletAsync(options));
    }

    /// <summary>
    /// Бронь без замороженных денег удерживать не за что: неявка её закрывает, но удержание не
    /// выписывается — иначе клуб получил бы выручку из воздуха.
    /// </summary>
    [Fact]
    public async Task NoShow_WithRetention_ChargesNothing_WhenTheHoldWasAlreadyReleased()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true, openShift: true);
        var booked = await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            var cancelled = await service.CancelOnlineAsync(
                booked.Response!.ReservationId, PlayerId, CancellationToken.None);
            Assert.True(cancelled.Succeeded);
        }

        Assert.Equal(0, await RunAsync(options, Start.AddMinutes(21)));

        await using var read = new PlatformDbContext(options);
        Assert.False(await read.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee));
        Assert.Equal(5_000, await WalletAsync(options));
    }

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task<int> RunAsync(DbContextOptions<PlatformDbContext> options, DateTimeOffset now)
    {
        await using var db = new PlatformDbContext(options);
        var clock = new FixedTimeProvider(now);
        var runner = new ReservationNoShowRunner(db, clock, new EfShiftService(db, clock));
        return await runner.RunOnceAsync(CancellationToken.None);
    }

    private static async Task<long> WalletAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerId, CancellationToken.None);
        return summary!.WalletBalance.MinorUnits;
    }

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
        bool openShift,
        int holdSeatAfterStartMinutes = 20)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Start });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Start });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000001", PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Start
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 5_000, CurrencyCode = "TJS", CreatedAtUtc = Start.AddDays(-1)
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId, Name = "Ночной", IsActive = true, CreatedAtUtc = Start
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

        if (openShift)
        {
            db.Shifts.Add(new ShiftEntity
            {
                ShiftId = ShiftId,
                OrganizationId = OrgId,
                BranchId = BranchId,
                OpenedByStaffUserId = StaffId,
                State = ShiftStateNames.Open,
                CurrencyCode = "TJS",
                OpenedAtUtc = Start.AddHours(-2)
            });
        }

        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
