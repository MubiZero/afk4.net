using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Клуб не ответил в срок — заявка снимается сама, а деньги возвращаются целиком.
///
/// Это не неявка: при неявке человек не пришёл, и филиал вправе оставить предоплату себе. Здесь
/// молчал клуб, и удерживать за это нечего — сколько бы филиал ни держал предоплату за неявку.
/// </summary>
public sealed class ReservationRequestExpiryRunnerTests
{
    private static readonly Guid OrgId = Guid.Parse("5cb1e0d2-0001-4001-8001-000000000001");
    private static readonly Guid BranchId = Guid.Parse("5cb1e0d2-0002-4002-8002-000000000002");
    private static readonly Guid ZoneId = Guid.Parse("5cb1e0d2-0003-4003-8003-000000000003");
    private static readonly Guid SeatId = Guid.Parse("5cb1e0d2-0004-4004-8004-000000000004");
    private static readonly Guid PlayerId = Guid.Parse("5cb1e0d2-0006-4006-8006-000000000006");
    private static readonly Guid TariffId = Guid.Parse("5cb1e0d2-0007-4007-8007-000000000007");
    private static readonly Guid TariffVersionId = Guid.Parse("5cb1e0d2-0008-4008-8008-000000000008");
    private static readonly Guid StaffId = Guid.Parse("5cb1e0d2-0009-4009-8009-000000000009");

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");
    private static readonly DateTimeOffset Start = Now.AddHours(4);

    /// <summary>Час по 10 за минуту — 600 замороженных единиц под заявку.</summary>
    private const long HoldAmount = 600;
    private const long TopUp = 5_000;

    [Fact]
    public async Task ExpiredRequest_IsCancelledAndTheMoneyComesBackWhole()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var booked = await BookAsync(options);

        Assert.Equal(TopUp - HoldAmount, await WalletAsync(options));
        Assert.Equal(1, await RunAsync(options, Now.AddMinutes(31)));

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(row => row.ReservationId == booked.ReservationId);
        Assert.Equal(ReservationStateNames.Cancelled, reservation.State);
        Assert.Equal(ReservationRequestExpiryRunner.CancelReason, reservation.CancelReason);
        Assert.Equal(Now.AddMinutes(31), reservation.CancelledAtUtc);
        Assert.Null(reservation.ConfirmedAtUtc);

        var balances = await LedgerBalanceProjector.GetClubBalancesAsync(db, PlayerId, CancellationToken.None);
        Assert.Equal(TopUp, balances.WalletMinorUnits);
        Assert.Equal(0, balances.HeldMinorUnits);
    }

    /// <summary>
    /// Филиал держит предоплату при неявке — но молчание клуба неявкой не становится. Удержания
    /// нет, деньги вернулись целиком.
    /// </summary>
    [Fact]
    public async Task ExpiredRequest_IsNeverANoShow_EvenWhenTheBranchKeepsPrepayment()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        await BookAsync(options);

        Assert.Equal(1, await RunAsync(options, Now.AddMinutes(31)));

        await using var db = new PlatformDbContext(options);
        Assert.False(await db.LedgerEntries.AnyAsync(
            entry => entry.EntryType == LedgerEntryTypeNames.ReservationNoShowFee));
        Assert.Equal(TopUp, await WalletAsync(options));
    }

    [Fact]
    public async Task BeforeTheDeadline_TheRequestIsLeftAlone()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var booked = await BookAsync(options);

        Assert.Equal(0, await RunAsync(options, Now.AddMinutes(29)));

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(row => row.ReservationId == booked.ReservationId);
        Assert.Equal(ReservationStateNames.Pending, reservation.State);
        Assert.Equal(TopUp - HoldAmount, await WalletAsync(options));
    }

    /// <summary>Клуб успел ответить — заявка больше не под таймером, деньги остаются занятыми.</summary>
    [Fact]
    public async Task ConfirmedRequest_IsNotTouchedAfterTheDeadline()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var booked = await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var confirmed = await new EfReservationService(db, new FixedTimeProvider(Now.AddMinutes(5))).ConfirmAsync(
                booked.ReservationId,
                StaffId,
                new ConfirmReservationRequest(OrgId, booked.Version),
                CancellationToken.None);
            Assert.True(confirmed.Succeeded);
        }

        Assert.Equal(0, await RunAsync(options, Now.AddMinutes(31)));

        await using var read = new PlatformDbContext(options);
        var reservation = await read.Reservations.SingleAsync(row => row.ReservationId == booked.ReservationId);
        Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
        Assert.Equal(TopUp - HoldAmount, await WalletAsync(options));
        Assert.False(await read.LedgerEntries.AnyAsync(entry => entry.EntryType == LedgerEntryTypeNames.Reversal));
    }

    /// <summary>Бронь, подтверждённую сразу (режим «подтверждаем сами»), таймер не видит вовсе.</summary>
    [Fact]
    public async Task AutoConfirmedBooking_HasNoDeadlineAndSurvives()
    {
        var options = NewOptions();
        await SeedAsync(options, acceptanceMode: BranchBookingAcceptanceModes.Auto);
        var booked = await BookAsync(options);
        Assert.Equal(ReservationStateNames.Confirmed, booked.State);

        Assert.Equal(0, await RunAsync(options, Now.AddDays(1)));
    }

    [Fact]
    public async Task SecondRun_ChangesNothing()
    {
        var options = NewOptions();
        await SeedAsync(options);
        await BookAsync(options);

        Assert.Equal(1, await RunAsync(options, Now.AddMinutes(31)));
        Assert.Equal(0, await RunAsync(options, Now.AddMinutes(45)));

        await using var db = new PlatformDbContext(options);
        Assert.Equal(1, await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.Reversal));
        Assert.Equal(TopUp, await WalletAsync(options));
    }

    /// <summary>
    /// Заявка без замороженных денег (филиал предоплаты не просит) всё равно снимается: слот
    /// нельзя держать занятым обещанием, на которое никто не ответил.
    /// </summary>
    [Fact]
    public async Task RequestWithoutFrozenMoney_IsStillCancelled()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var booked = await BookAsync(options, withPrepayment: false);

        Assert.Equal(1, await RunAsync(options, Now.AddMinutes(31)));

        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(row => row.ReservationId == booked.ReservationId);
        Assert.Equal(ReservationStateNames.Cancelled, reservation.State);
        Assert.Equal(TopUp, await WalletAsync(options));
        Assert.False(await db.LedgerEntries.AnyAsync(entry => entry.EntryType == LedgerEntryTypeNames.Reversal));
    }

    /// <summary>Отменённую руками заявку таймер второй раз не отменяет и денег дважды не возвращает.</summary>
    [Fact]
    public async Task RequestCancelledByThePlayer_IsNotCancelledAgain()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var booked = await BookAsync(options);

        await using (var db = new PlatformDbContext(options))
        {
            var cancelled = await new EfReservationService(db, new FixedTimeProvider(Now.AddMinutes(2)))
                .CancelOnlineAsync(booked.ReservationId, PlayerId, CancellationToken.None);
            Assert.True(cancelled.Succeeded);
        }

        Assert.Equal(0, await RunAsync(options, Now.AddMinutes(31)));

        await using var read = new PlatformDbContext(options);
        Assert.Equal(1, await read.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.Reversal));
        Assert.Equal(TopUp, await WalletAsync(options));
    }

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task<int> RunAsync(DbContextOptions<PlatformDbContext> options, DateTimeOffset now)
    {
        await using var db = new PlatformDbContext(options);
        return await new ReservationRequestExpiryRunner(db, new FixedTimeProvider(now))
            .RunOnceAsync(CancellationToken.None);
    }

    private static async Task<long> WalletAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerId, CancellationToken.None);
        return summary!.WalletBalance.MinorUnits;
    }

    private static async Task<ReservationDto> BookAsync(
        DbContextOptions<PlatformDbContext> options,
        bool withPrepayment = true)
    {
        await using var db = new PlatformDbContext(options);
        var result = await new EfReservationService(db, new FixedTimeProvider(Now)).CreateOnlineAsync(
            PlayerId,
            OrgId,
            BranchId,
            new CreatePlayerReservationRequest(
                SeatId, Start, Start.AddHours(1), null, withPrepayment ? TariffVersionId : null),
            CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Response!;
    }

    private static async Task SeedAsync(
        DbContextOptions<PlatformDbContext> options,
        string acceptanceMode = BranchBookingAcceptanceModes.Manual,
        bool keepPrepaymentOnNoShow = false)
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
            ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId,
            Name = "ПК-01", SortOrder = 10, CreatedAtUtc = Now
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
            AmountMinorUnits = TopUp, CurrencyCode = "TJS", CreatedAtUtc = Now.AddDays(-1)
        });

        var settings = BranchBookingSettingsTestData.AcceptsAnyGuest(OrgId, BranchId, Now);
        settings.AcceptanceMode = acceptanceMode;
        settings.RespondWithinMinutes = 30;
        settings.KeepPrepaymentOnNoShow = keepPrepaymentOnNoShow;
        db.BranchBookingSettings.Add(settings);

        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
