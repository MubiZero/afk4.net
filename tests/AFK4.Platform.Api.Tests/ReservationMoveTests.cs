using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Перенос собственной брони игроком.
///
/// До него переносили отменой и повторной бронью: место на те секунды, что человек ищет новое
/// время, уходило в общий доступ, а замороженная предоплата возвращалась и замораживалась заново —
/// и если денег за эти секунды не осталось, второй брони уже не было.
/// </summary>
public sealed class ReservationMoveTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherSeatId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
    private static readonly Guid PlayerId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
    private static readonly Guid TariffId = Guid.Parse("11111111-1111-4111-1111-111111111111");
    private static readonly Guid TariffVersionId = Guid.Parse("22222222-2222-4222-2222-222222222222");
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
        db.Seats.Add(new SeatEntity { SeatId = OtherSeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-02", SortOrder = 20, CreatedAtUtc = Start });
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
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(OrgId, BranchId, Start));
        await db.SaveChangesAsync();
    }

    private static async Task<ReservationDto> BookAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var result = await service.CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(SeatId, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Response!;
    }

    private static async Task<ReservationServiceResult<ReservationDto>> MoveAsync(
        DbContextOptions<PlatformDbContext> options,
        Guid reservationId,
        DateTimeOffset startsAtUtc,
        Guid? seatId = null,
        Guid? playerAccountId = null)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        return await service.MoveOnlineAsync(
            reservationId, playerAccountId ?? PlayerId, startsAtUtc, seatId, null, CancellationToken.None);
    }

    private static async Task<long> WalletAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerId, CancellationToken.None);
        return summary!.WalletBalance.MinorUnits;
    }

    [Fact]
    public async Task Moving_ChangesTheTimeAndKeepsTheSeat()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        var moved = await MoveAsync(options, booked.ReservationId, Start.AddHours(3));

        Assert.True(moved.Succeeded);
        Assert.Equal(Start.AddHours(3), moved.Response!.StartsAtUtc);
        Assert.Equal(Start.AddHours(4), moved.Response.EndsAtUtc);
        Assert.Equal(SeatId, moved.Response.SeatId);
        Assert.Equal(60, moved.Response.DurationMinutes);
    }

    // Перенос на час позже не должен требовать второй оплаты того же часа: старая заморозка
    // возвращается вместе с переносом, и денег нужно ровно на разницу.
    [Fact]
    public async Task Moving_DoesNotFreezeTheMoneyTwice()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 2_000);
        var booked = await BookAsync(options);
        Assert.Equal(500, await WalletAsync(options));

        var moved = await MoveAsync(options, booked.ReservationId, Start.AddHours(3));

        Assert.True(moved.Succeeded);
        // Заморожено по-прежнему 1500 из 2000, а не 3000 из 2000.
        Assert.Equal(500, await WalletAsync(options));
    }

    [Fact]
    public async Task Moving_CanAlsoChangeTheSeat()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        var moved = await MoveAsync(options, booked.ReservationId, Start.AddHours(3), OtherSeatId);

        Assert.True(moved.Succeeded);
        Assert.Equal(OtherSeatId, moved.Response!.SeatId);
    }

    // Место занято чужой бронью — это не ошибка запроса, а «выберите другое время».
    [Fact]
    public async Task Moving_OntoATakenSlot_IsAConflict()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 20_000);
        var first = await BookAsync(options);
        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            var second = await service.CreateOnlineAsync(
                PlayerId, OrgId, BranchId,
                new CreatePlayerReservationRequest(SeatId, Start.AddHours(5), Start.AddHours(6), null, TariffVersionId),
                CancellationToken.None);
            Assert.True(second.Succeeded);
        }

        var moved = await MoveAsync(options, first.ReservationId, Start.AddHours(5));

        Assert.False(moved.Succeeded);
        Assert.True(moved.Conflict);
    }

    // Чужая бронь отвечает «не найдено», а не «нельзя»: иначе отказ подсказывал бы, что она есть.
    [Fact]
    public async Task MovingSomeoneElsesReservation_IsNotFound()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        var moved = await MoveAsync(options, booked.ReservationId, Start.AddHours(3), playerAccountId: Guid.NewGuid());

        Assert.True(moved.NotFound);
    }

    [Fact]
    public async Task MovingACancelledReservation_IsRejected()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);
        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            await service.CancelOnlineAsync(booked.ReservationId, PlayerId, CancellationToken.None);
        }

        var moved = await MoveAsync(options, booked.ReservationId, Start.AddHours(3));

        Assert.False(moved.Succeeded);
        Assert.False(moved.NotFound);
    }

    // Перенос на то же время и место — не ошибка, а «ничего не изменилось».
    [Fact]
    public async Task MovingNowhere_IsAccepted()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000);
        var booked = await BookAsync(options);

        var moved = await MoveAsync(options, booked.ReservationId, Start, SeatId);

        Assert.True(moved.Succeeded);
        Assert.Equal(3_500, await WalletAsync(options));
    }
}
