using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Заморозка денег под бронь: слот занят — значит и сумма занята. Холд никогда не превращается в
/// оплату, он только снимается: при посадке сессию считает обычный биллинг, и деньги не должны
/// уходить дважды.
/// </summary>
public sealed class ReservationHoldTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
    private static readonly Guid TariffId = Guid.Parse("11111111-1111-4111-1111-111111111111");
    private static readonly Guid TariffVersionId = Guid.Parse("22222222-2222-4222-2222-222222222222");
    private static readonly Guid StaffId = Guid.Parse("99999999-9999-4999-9999-999999999999");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-18T16:00:00Z");

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task SeedAsync(DbContextOptions<PlatformDbContext> options, long walletMinor)
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
            AmountMinorUnits = walletMinor, CurrencyCode = "TJS", CreatedAtUtc = Start
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
        // Файл про заморозку денег, а не про правила приёма: филиал берёт бронь и без предоплаты.
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(OrgId, BranchId, Start));
        await db.SaveChangesAsync();
    }

    private static async Task<ReservationServiceResult<ReservationDto>> BookAsync(
        DbContextOptions<PlatformDbContext> options,
        Guid? tariffVersionId = null,
        DateTimeOffset? startsAt = null)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var from = startsAt ?? Start;
        return await service.CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(SeatId, from, from.AddHours(1), null, tariffVersionId ?? TariffVersionId),
            CancellationToken.None);
    }

    /// <summary>Доступный баланс кошелька — журнал уже учитывает холд, отдельного знания не нужно.</summary>
    private static async Task<long> WalletAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerId, CancellationToken.None);
        return summary!.WalletBalance.MinorUnits;
    }

    [Fact]
    public async Task Booking_FreezesTheEstimatedAmount()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);

        var result = await BookAsync(options);

        Assert.True(result.Succeeded);
        // 1500 из 5000 заморожено — на кошельке остаётся 3500, и второй бронью их уже не потратить.
        Assert.Equal(3_500, await WalletAsync(options));
        Assert.Equal(ReservationStateNames.Confirmed, result.Response!.State);
    }

    // «Бронь только с деньгами»: без суммы бронь не висит в ожидании, а отклоняется — иначе слот
    // занят, а обеспечения под ним нет.
    [Fact]
    public async Task Booking_IsRejected_WhenTheWalletCannotCoverIt()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 1_000);

        var result = await BookAsync(options);

        Assert.False(result.Succeeded);
        Assert.Equal("insufficient_funds", result.Error);
        Assert.Equal(1_000, await WalletAsync(options));

        await using var db = new PlatformDbContext(options);
        Assert.False(await db.Reservations.AnyAsync());
    }

    [Fact]
    public async Task Cancelling_ReturnsTheMoney()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            var cancelled = await service.CancelOnlineAsync(
                booked.Response!.ReservationId, PlayerId, CancellationToken.None);
            Assert.True(cancelled.Succeeded);
        }

        Assert.Equal(5_000, await WalletAsync(options));
    }

    // Повторная отмена не должна вернуть деньги во второй раз — иначе игрок «зарабатывает»
    // на кнопке отмены.
    [Fact]
    public async Task Cancelling_Twice_ReturnsTheMoneyOnce()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = new PlatformDbContext(options);
            var service = new EfReservationService(db, TimeProvider.System);
            await service.CancelOnlineAsync(booked.Response!.ReservationId, PlayerId, CancellationToken.None);
        }

        Assert.Equal(5_000, await WalletAsync(options));
    }

    // Посадка снимает холд: дальше время считает обычный биллинг сессии, и списание не двойное.
    [Fact]
    public async Task Seating_ReleasesTheHold()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            var seated = await service.SeatAsync(
                booked.Response!.ReservationId,
                StaffId,
                new SeatReservationRequest(OrgId, booked.Response.Version),
                CancellationToken.None);
            Assert.True(seated.Succeeded);
        }

        Assert.Equal(5_000, await WalletAsync(options));
    }

    [Fact]
    public async Task Booking_WithoutATariff_FreezesNothing()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 1_000);

        var result = await BookAsync(options, tariffVersionId: Guid.Empty);

        // Пустой тариф — это «выбора нет»: бронь живёт как раньше, её посчитают на стойке.
        Assert.False(result.Succeeded);

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var unpriced = await service.CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(SeatId, Start, Start.AddHours(1), null),
            CancellationToken.None);

        Assert.True(unpriced.Succeeded);
        Assert.Equal(1_000, await WalletAsync(options));
    }

    [Fact]
    public async Task NoShow_ReturnsTheMoneyAndFreesTheSeat()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        int handled;
        await using (var db = new PlatformDbContext(options))
        {
            var runner = NewRunner(db, Start.AddMinutes(21));
            handled = await runner.RunOnceAsync(CancellationToken.None);
        }

        Assert.Equal(1, handled);
        Assert.Equal(5_000, await WalletAsync(options));

        await using var read = new PlatformDbContext(options);
        var reservation = await read.Reservations.SingleAsync(r => r.ReservationId == booked.Response!.ReservationId);
        Assert.Equal(ReservationStateNames.Cancelled, reservation.State);
        Assert.Equal(ReservationNoShowRunner.CancelReason, reservation.CancelReason);
    }

    // Опоздание на десять минут — не неявка: место и деньги ещё держатся.
    [Fact]
    public async Task NoShow_WaitsOutTheGracePeriod()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var runner = NewRunner(db, Start.AddMinutes(10));
            Assert.Equal(0, await runner.RunOnceAsync(CancellationToken.None));
        }

        Assert.Equal(3_500, await WalletAsync(options));
    }

    // Игрок сел вовремя — фоновая задача не должна ни отменять бронь, ни возвращать деньги
    // второй раз поверх снятого при посадке холда.
    [Fact]
    public async Task NoShow_SkipsSeatedReservations()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            await service.SeatAsync(
                booked.Response!.ReservationId, StaffId,
                new SeatReservationRequest(OrgId, booked.Response.Version), CancellationToken.None);
        }

        await using (var db = new PlatformDbContext(options))
        {
            var runner = NewRunner(db, Start.AddHours(2));
            Assert.Equal(0, await runner.RunOnceAsync(CancellationToken.None));
        }

        Assert.Equal(5_000, await WalletAsync(options));
    }

    // Компания из трёх не пришла. Фоновая задача разбирает брони по одной и ничего не знает про
    // группы — ровно поэтому холд и стоит на каждом месте отдельно: деньги возвращаются все,
    // а специального кода для компаний не потребовалось.
    [Fact]
    public async Task NoShow_ReturnsTheMoneyForEverySeatOfACompany()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 10_000);
        var group = await BookGroupAsync(options, seatCount: 3);
        Assert.Equal(5_500, await WalletAsync(options));

        int handled;
        await using (var db = new PlatformDbContext(options))
        {
            var runner = NewRunner(db, Start.AddMinutes(21));
            handled = await runner.RunOnceAsync(CancellationToken.None);
        }

        Assert.Equal(3, handled);
        Assert.Equal(10_000, await WalletAsync(options));

        await using var read = new PlatformDbContext(options);
        var rows = await read.Reservations
            .Where(r => r.ReservationGroupId == group.Response![0].ReservationGroupId)
            .ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(ReservationStateNames.Cancelled, r.State));
        Assert.All(rows, r => Assert.Equal(ReservationNoShowRunner.CancelReason, r.CancelReason));
    }

    private static async Task<ReservationServiceResult<IReadOnlyList<ReservationDto>>> BookGroupAsync(
        DbContextOptions<PlatformDbContext> options,
        int seatCount)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        return await service.CreateOnlineGroupAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationGroupRequest(seatCount, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);
    }

    // Место филиал держит двадцать минут (значение по умолчанию), поэтому «не приехал» — это
    // двадцать первая минута, а не шестнадцатая.
    private static ReservationNoShowRunner NewRunner(PlatformDbContext db, DateTimeOffset now)
    {
        var clock = new FixedTimeProvider(now);
        return new ReservationNoShowRunner(db, clock, new EfShiftService(db, clock));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
