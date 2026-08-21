using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Судьбу брони из приложения решает филиал, а не код.
///
/// До Ф6 условие подтверждения было зашито: «кошелёк больше нуля и нет долга». Теперь это
/// поведение режима <c>auto</c> — одного из трёх, — а рядом живут «смотрит администратор» и
/// «брони закрыты», требование предоплаты с новых гостей и потолок активных броней у новичка.
/// </summary>
public sealed class EfReservationAcceptanceModeTests
{
    private static readonly Guid OrgId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d01");
    private static readonly Guid BranchId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d02");
    private static readonly Guid ZoneId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d03");
    private static readonly Guid SeatId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d04");
    private static readonly Guid OtherSeatId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d05");
    private static readonly Guid PlayerId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d06");
    private static readonly Guid TariffId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d07");
    private static readonly Guid TariffVersionId = Guid.Parse("6f4a1f1c-9c1d-4c2f-9f6a-2a0f3b7c1d08");

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");
    private static readonly DateTimeOffset Start = Now.AddHours(4);

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static TimeProvider Clock() => new FixedTimeProvider(Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task SeedAsync(
        DbContextOptions<PlatformDbContext> options,
        long walletMinor = 100_000,
        long debtMinor = 0,
        int endedVisits = 0,
        BranchBookingSettingsEntity? settings = null)
    {
        await using var db = new PlatformDbContext(options);
        db.Organizations.Add(new OrganizationEntity { OrganizationId = OrgId, Name = "Клуб", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId, OrganizationId = OrgId, Slug = "branch-01", Name = "Филиал",
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Now });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "ПК-01", SortOrder = 10, CreatedAtUtc = Now });
        db.Seats.Add(new SeatEntity { SeatId = OtherSeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "ПК-02", SortOrder = 20, CreatedAtUtc = Now });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000001", PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Now
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId, Name = "Дневной", IsActive = true, CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId, TariffId = TariffId, OrganizationId = OrgId, BranchId = BranchId,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 10,
            MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1, EffectiveFromUtc = Now.AddYears(-1)
        });

        if (walletMinor != 0)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
                EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
                AmountMinorUnits = walletMinor, CurrencyCode = "TJS", CreatedAtUtc = Now.AddDays(-1)
            });
        }

        if (debtMinor != 0)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
                EntryType = LedgerEntryTypeNames.PostpaidDebt, AccountType = LedgerAccountTypeNames.Debt,
                AmountMinorUnits = debtMinor, CurrencyCode = "TJS", CreatedAtUtc = Now.AddDays(-1)
            });
        }

        for (var index = 0; index < endedVisits; index++)
        {
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, SeatId = SeatId,
                DeviceId = Guid.NewGuid(), PlayerAccountId = PlayerId, PlayerKind = "member",
                State = SessionStateNames.Ended, RequestedAtUtc = Now.AddDays(-10 + index),
                StartedAtUtc = Now.AddDays(-10 + index), EndedAtUtc = Now.AddDays(-10 + index).AddHours(2)
            });
        }

        if (settings is not null)
        {
            db.BranchBookingSettings.Add(settings);
        }

        await db.SaveChangesAsync();
    }

    private static BranchBookingSettingsEntity Settings(
        string acceptanceMode = BranchBookingAcceptanceModes.Auto,
        bool requirePrepaymentFromNewGuests = true,
        int maxActiveReservationsForNewGuests = 1,
        int regularAfterVisits = 3) => new()
        {
            BranchId = BranchId,
            OrganizationId = OrgId,
            AcceptanceMode = acceptanceMode,
            RespondWithinMinutes = BranchBookingSettingsDefaults.RespondWithinMinutes,
            RequirePrepaymentFromNewGuests = requirePrepaymentFromNewGuests,
            MaxActiveReservationsForNewGuests = maxActiveReservationsForNewGuests,
            RegularAfterVisits = regularAfterVisits,
            HoldSeatAfterStartMinutes = BranchBookingSettingsDefaults.HoldSeatAfterStartMinutes,
            KeepPrepaymentOnNoShow = BranchBookingSettingsDefaults.KeepPrepaymentOnNoShow,
            UpdatedAtUtc = Now,
            UpdatedByStaffUserId = Guid.NewGuid()
        };

    private static async Task<ReservationServiceResult<ReservationDto>> BookAsync(
        DbContextOptions<PlatformDbContext> options,
        Guid? tariffVersionId,
        Guid? seatId = null,
        DateTimeOffset? startsAtUtc = null)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, Clock());
        var start = startsAtUtc ?? Start;
        return await service.CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(seatId ?? SeatId, start, start.AddHours(1), null, tariffVersionId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Off_RefusesBooking_BecauseTheClubClosedTheApp()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings(BranchBookingAcceptanceModes.Off));

        var result = await BookAsync(options, TariffVersionId);

        Assert.False(result.Succeeded);
        Assert.Equal(PlayerBookingRules.BookingDisabledCode, result.Error);
    }

    [Fact]
    public async Task Manual_KeepsBookingPending_EvenWhenMoneyIsFrozen()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings(BranchBookingAcceptanceModes.Manual));

        var result = await BookAsync(options, TariffVersionId);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Pending, result.Response!.State);

        // Заявку смотрит администратор, но деньги уже заморожены: слот занят с той же секунды.
        await using var db = new PlatformDbContext(options);
        Assert.True(await db.LedgerEntries.AnyAsync(entry => entry.EntryType == ReservationHold.EntryType));
    }

    [Fact]
    public async Task Auto_ConfirmsPrepaidBooking()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings());

        var result = await BookAsync(options, TariffVersionId);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Confirmed, result.Response!.State);
    }

    [Fact]
    public async Task Auto_ConfirmsUnpaidBooking_WhenWalletIsPositive()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings(requirePrepaymentFromNewGuests: false));

        var result = await BookAsync(options, tariffVersionId: null);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Confirmed, result.Response!.State);
    }

    [Fact]
    public async Task Auto_KeepsUnpaidBookingPending_WhenWalletIsEmpty()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 0, settings: Settings(requirePrepaymentFromNewGuests: false));

        var result = await BookAsync(options, tariffVersionId: null);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Pending, result.Response!.State);
    }

    [Fact]
    public async Task Auto_KeepsUnpaidBookingPending_WhenPlayerIsInDebt()
    {
        var options = NewOptions();
        await SeedAsync(options, debtMinor: 3_000, settings: Settings(requirePrepaymentFromNewGuests: false));

        var result = await BookAsync(options, tariffVersionId: null);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Pending, result.Response!.State);
    }

    [Fact]
    public async Task NewGuest_WithoutPrepayment_IsRefused()
    {
        var options = NewOptions();
        await SeedAsync(options, endedVisits: 2, settings: Settings());

        var result = await BookAsync(options, tariffVersionId: null);

        Assert.False(result.Succeeded);
        Assert.Equal(PlayerBookingRules.PrepaymentRequiredCode, result.Error);
    }

    [Fact]
    public async Task RegularGuest_BooksWithoutPrepayment_BecauseTheVisitThresholdLiftedIt()
    {
        var options = NewOptions();
        await SeedAsync(options, endedVisits: 3, settings: Settings());

        var result = await BookAsync(options, tariffVersionId: null);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Confirmed, result.Response!.State);
    }

    [Fact]
    public async Task NewGuest_SecondActiveBooking_IsRefused()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings());

        Assert.True((await BookAsync(options, TariffVersionId)).Succeeded);
        var second = await BookAsync(options, TariffVersionId, OtherSeatId, Start.AddDays(1));

        Assert.False(second.Succeeded);
        Assert.Equal(PlayerBookingRules.ActiveReservationLimitCode, second.Error);
    }

    [Fact]
    public async Task RegularGuest_SecondActiveBooking_IsAllowed()
    {
        var options = NewOptions();
        await SeedAsync(options, endedVisits: 3, settings: Settings());

        Assert.True((await BookAsync(options, TariffVersionId)).Succeeded);
        var second = await BookAsync(options, TariffVersionId, OtherSeatId, Start.AddDays(1));

        Assert.True(second.Succeeded);
    }

    [Fact]
    public async Task NewGuest_GroupBooking_CountsAsOneBooking()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings());

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, Clock());
        var result = await service.CreateOnlineGroupAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationGroupRequest(4, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);

        // Четыре места — это одна бронь на компанию, а не четыре брони: потолок в одну бронь
        // не должен запрещать новичку прийти вчетвером.
        Assert.True(result.Succeeded);
        Assert.Equal(4, result.Response!.Count);
    }

    [Fact]
    public async Task NewGuest_CancelledBooking_FreesTheLimit()
    {
        var options = NewOptions();
        await SeedAsync(options, settings: Settings());

        var first = await BookAsync(options, TariffVersionId);
        Assert.True(first.Succeeded);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, Clock());
            var cancelled = await service.CancelOnlineAsync(
                first.Response!.ReservationId, PlayerId, CancellationToken.None);
            Assert.True(cancelled.Succeeded);
        }

        var second = await BookAsync(options, TariffVersionId, OtherSeatId, Start.AddDays(1));

        Assert.True(second.Succeeded);
    }

    [Fact]
    public async Task BranchWithoutSettings_BehavesLikeDefaults()
    {
        var options = NewOptions();
        await SeedAsync(options);

        // Филиал ничего не настраивал: приём включён, предоплата с новичка нужна.
        Assert.Equal(ReservationStateNames.Confirmed, (await BookAsync(options, TariffVersionId)).Response!.State);

        var options2 = NewOptions();
        await SeedAsync(options2);
        var unpaid = await BookAsync(options2, tariffVersionId: null);
        Assert.False(unpaid.Succeeded);
        Assert.Equal(PlayerBookingRules.PrepaymentRequiredCode, unpaid.Error);
    }
}
