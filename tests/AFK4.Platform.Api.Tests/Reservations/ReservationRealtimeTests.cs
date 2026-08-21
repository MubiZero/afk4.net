using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Reservations;

/// <summary>
/// Полоса заявок узнаёт об изменениях сразу, а не следующим опросом.
///
/// До сих пор администратор видел чужое решение через несколько секунд, а два администратора на
/// разных машинах какое-то время видели разное. Хуже того, решения принимают и таймеры — срок
/// ответа истекает сам, — и о них узнать было неоткуда, кроме следующего опроса: заявка просто
/// исчезала из полосы, и никто не мог сказать, кто её снял.
/// </summary>
public sealed class ReservationRealtimeTests
{
    private static readonly Guid OrgId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000001");
    private static readonly Guid BranchId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000002");
    private static readonly Guid ZoneId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000003");
    private static readonly Guid SeatId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000004");
    private static readonly Guid PlayerId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000005");
    private static readonly Guid TariffId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000006");
    private static readonly Guid TariffVersionId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000007");
    private static readonly Guid StaffId = Guid.Parse("8a71c3d0-2f14-4a0b-9c31-71bb0d000008");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-20T19:00:00Z");
    private static readonly DateTimeOffset Now = Start.AddHours(-2);

    [Fact]
    public async Task Rejecting_TellsTheDeskAtOnce()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var reservationId = await RequestAsync(options);
        var notifier = new RecordingReservationNotifier();

        await using (var db = new PlatformDbContext(options))
        {
            await new EfReservationService(db, Clock(Now), reservationNotifier: notifier)
                .RejectAsync(reservationId, StaffId, new RejectReservationRequest(OrgId, RejectReasonCodes.NoSeats), CancellationToken.None);
        }

        var change = Assert.Single(notifier.Changes);
        Assert.Equal(ReservationChangeKinds.Rejected, change.Kind);
        Assert.Equal(ReservationStateNames.Rejected, change.State);
        Assert.Equal(reservationId, change.ReservationId);
        Assert.Equal(BranchId, change.BranchId);
    }

    /// <summary>
    /// Решение таймера — то самое, о котором стойке узнать больше неоткуда: заявка исчезала из
    /// полосы, и никто не мог сказать, кто её снял.
    /// </summary>
    [Fact]
    public async Task TheClubsSilenceRunningOut_TellsTheDeskToo()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var reservationId = await RequestAsync(options);
        var notifier = new RecordingReservationNotifier();

        await using (var db = new PlatformDbContext(options))
        {
            await new ReservationRequestExpiryRunner(db, Clock(Start.AddMinutes(1)), notifier)
                .RunOnceAsync(CancellationToken.None);
        }

        var change = Assert.Single(notifier.Changes);
        Assert.Equal(ReservationChangeKinds.Expired, change.Kind);
        Assert.Equal(ReservationStateNames.Cancelled, change.State);
    }

    [Fact]
    public async Task NoShow_TellsTheDeskToo()
    {
        var options = NewOptions();
        await SeedAsync(options);
        await ConfirmedBookingAsync(options);
        var notifier = new RecordingReservationNotifier();

        await using (var db = new PlatformDbContext(options))
        {
            var clock = Clock(Start.AddMinutes(21));
            await new ReservationNoShowRunner(db, clock, new EfShiftService(db, clock), notifier)
                .RunOnceAsync(CancellationToken.None);
        }

        var change = Assert.Single(notifier.Changes);
        Assert.Equal(ReservationChangeKinds.NoShow, change.Kind);
        Assert.Equal(ReservationStateNames.NoShow, change.State);
    }

    /// <summary>
    /// Событие уходит только после того, как изменение легло в базу. Иначе откатившаяся правка
    /// оставила бы на экранах решение, которого не было, — и стойка спорила бы с базой.
    /// </summary>
    [Fact]
    public async Task NothingIsAnnouncedWhenNothingChanged()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var reservationId = await RequestAsync(options);
        var notifier = new RecordingReservationNotifier();

        await using (var db = new PlatformDbContext(options))
        {
            // Выдуманная причина отвергается до записи — значит и рассказывать не о чем.
            var result = await new EfReservationService(db, Clock(Now), reservationNotifier: notifier)
                .RejectAsync(reservationId, StaffId, new RejectReservationRequest(OrgId, "because_i_said_so"), CancellationToken.None);
            Assert.False(result.Succeeded);
        }

        Assert.Empty(notifier.Changes);
    }

    private sealed class RecordingReservationNotifier : IReservationChangeNotifier
    {
        public List<ReservationChangedDto> Changes { get; } = [];

        public Task NotifyAsync(ReservationChangedDto change, CancellationToken cancellationToken)
        {
            Changes.Add(change);
            return Task.CompletedTask;
        }
    }

    private static TimeProvider Clock(DateTimeOffset now) => new FixedTimeProvider(now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;

    /// <summary>Заявка из приложения, которую стойка ещё не смотрела.</summary>
    private static async Task<Guid> RequestAsync(DbContextOptions<PlatformDbContext> options)
    {
        var id = await ConfirmedBookingAsync(options);
        await using var db = new PlatformDbContext(options);
        var reservation = await db.Reservations.SingleAsync(row => row.ReservationId == id);
        reservation.State = ReservationStateNames.Pending;
        reservation.ConfirmedAtUtc = null;
        reservation.RespondByUtc = Start;
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> ConfirmedBookingAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var result = await new EfReservationService(db, Clock(Now)).CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(SeatId, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Response!.ReservationId;
    }

    private static async Task SeedAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Now });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Now });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = TestPhones.Next(), PreferredLocale = "ru", IsActive = true, CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
            EntryType = AFK4.Shared.Contracts.Billing.LedgerEntryTypeNames.TopUp,
            AccountType = AFK4.Shared.Contracts.Billing.LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 20_000, CurrencyCode = "TJS", CreatedAtUtc = Now.AddDays(-1)
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId, Name = "Вечерний", IsActive = true, CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId, TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1, EffectiveFromUtc = Now.AddYears(-1)
        });

        var settings = BranchBookingSettingsTestData.AcceptsAnyGuest(OrgId, BranchId, Now);
        settings.KeepPrepaymentOnNoShow = false;
        db.BranchBookingSettings.Add(settings);

        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, OpenedByStaffUserId = StaffId,
            State = ShiftStateNames.Open, CurrencyCode = "TJS", OpenedAtUtc = Now.AddHours(-1)
        });

        await db.SaveChangesAsync();
    }
}
