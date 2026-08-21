using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Клуб отказывает в заявке — и говорит почему.
///
/// До сих пор отказ делался той же кнопкой «Отменить», что и всё остальное, со свободным текстом
/// в поле причины. Для игрока это значило, что его собственная отмена и отказ клуба —
/// неразличимые события, а причину он не узнавал вовсе: ни приложение, ни веб `CancelReason` не
/// читают. Человек видел, что брони больше нет, и не видел, почему.
///
/// Отказ — не отмена ещё и по деньгам и по репутации: игрок ничего не отменял и никуда не не
/// приехал, поэтому предоплата возвращается целиком всегда, а в его сетевые числа отказ не
/// попадает ни при каких настройках филиала.
/// </summary>
public sealed class ReservationRejectTests
{
    private static readonly Guid OrgId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001001");
    private static readonly Guid BranchId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001002");
    private static readonly Guid ZoneId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001003");
    private static readonly Guid SeatId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001004");
    private static readonly Guid PlayerId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001005");
    private static readonly Guid TariffId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001006");
    private static readonly Guid TariffVersionId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001007");
    private static readonly Guid StaffId = Guid.Parse("2c9f41aa-5b31-4d7e-9a11-7d2c5e001008");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-10T19:00:00Z");
    private static readonly DateTimeOffset Now = Start.AddHours(-3);

    private const long HoldAmount = 1_500;

    [Fact]
    public async Task Reject_PutsTheClubsReasonOnTheRequest_NotACancellation()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);

        var result = await RejectAsync(options, request, RejectReasonCodes.NoSeats, "Все места заняты турниром");

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Rejected, result.Response!.State);
        Assert.Equal(RejectReasonCodes.NoSeats, result.Response.RejectReasonCode);
        Assert.Equal("Все места заняты турниром", result.Response.RejectReasonNote);
        Assert.Equal(Now, result.Response.RejectedAtUtc);

        await using var db = new PlatformDbContext(options);
        var stored = await db.Reservations.SingleAsync(r => r.ReservationId == request);
        // Отмена тут ни при чём: её колонки пусты, иначе отказ клуба снова станет неотличим от
        // того, что игрок передумал.
        Assert.Null(stored.CancelledAtUtc);
        Assert.Equal(string.Empty, stored.CancelReason);
        Assert.Equal(StaffId, stored.UpdatedByStaffUserId);
    }

    /// <summary>
    /// Клуб отказал — деньги возвращаются целиком и всегда. Удержание платит за неявку игрока, а
    /// не за решение клуба, и настройка «оставлять предоплату» здесь не при чём.
    /// </summary>
    [Fact]
    public async Task Reject_ReturnsTheMoneyWhole_EvenWhereNoShowsAreCharged()
    {
        var options = NewOptions();
        await SeedAsync(options, keepPrepaymentOnNoShow: true);
        var request = await RequestAsync(options);
        Assert.Equal(5_000 - HoldAmount, await WalletAsync(options));

        await RejectAsync(options, request, RejectReasonCodes.Maintenance, null);

        Assert.Equal(5_000, await WalletAsync(options));
        await using var db = new PlatformDbContext(options);
        Assert.False(await db.LedgerEntries.AnyAsync(
            e => e.EntryType == LedgerEntryTypeNames.ReservationNoShowFee));
    }

    /// <summary>Отказ клуба в сетевые числа игрока не попадает: он ничего не нарушал.</summary>
    [Fact]
    public async Task Reject_DoesNotCountAsANoShow()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);
        await RejectAsync(options, request, RejectReasonCodes.Event, null);

        await using var db = new PlatformDbContext(options);
        Assert.Equal(0, await db.Reservations.CountAsync(
            r => r.State == ReservationStateNames.NoShow));
    }

    /// <summary>
    /// Код «своими словами» без слов — это тот же пустой отказ, от которого уходили: игрок снова
    /// не узнаёт причину, а статистика получает мусорную корзину вместо ответа.
    /// </summary>
    [Fact]
    public async Task Reject_WithOtherReason_RequiresWords()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);

        var result = await RejectAsync(options, request, RejectReasonCodes.Other, "   ");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Reject_WithAnInventedReason_IsRefused()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);

        var result = await RejectAsync(options, request, "because_i_said_so", null);

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Отказать можно в заявке, которую ещё не приняли. Подтверждённую бронь клуб отменяет — это
    /// другое действие и другой разговор с человеком, которому уже пообещали место.
    /// </summary>
    [Fact]
    public async Task Reject_OnlyAppliesToARequestNobodyAnsweredYet()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);
        await SetStateAsync(options, request, ReservationStateNames.Confirmed);

        var result = await RejectAsync(options, request, RejectReasonCodes.NoSeats, null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Reject_Twice_KeepsTheFirstAnswer()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);

        await RejectAsync(options, request, RejectReasonCodes.NoSeats, "первый ответ");
        var second = await RejectAsync(options, request, RejectReasonCodes.Maintenance, "второй ответ");

        Assert.True(second.Succeeded);
        Assert.Equal(RejectReasonCodes.NoSeats, second.Response!.RejectReasonCode);
        Assert.Equal("первый ответ", second.Response.RejectReasonNote);
    }

    /// <summary>Отказ освобождает место так же, как отмена: зал не держат под несостоявшуюся заявку.</summary>
    [Fact]
    public async Task Reject_FreesTheSeat()
    {
        var options = NewOptions();
        await SeedAsync(options);
        var request = await RequestAsync(options);
        await RejectAsync(options, request, RejectReasonCodes.NoSeats, null);

        await using var db = new PlatformDbContext(options);
        var bookable = await BranchCapacity.LoadBookableSeatIdsAsync(db, OrgId, BranchId, CancellationToken.None);
        var busy = await BranchCapacity.CountOccupiedAsync(
            db, OrgId, BranchId, bookable, Start, Start.AddHours(1), Now, CancellationToken.None);

        Assert.Equal(0, busy);
    }

    private static async Task<ReservationServiceResult<ReservationDto>> RejectAsync(
        DbContextOptions<PlatformDbContext> options, Guid reservationId, string reasonCode, string? note)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, new FixedTimeProvider(Now));
        return await service.RejectAsync(
            reservationId,
            StaffId,
            new RejectReservationRequest(OrgId, reasonCode, note),
            CancellationToken.None);
    }

    /// <summary>Заявка из приложения, которую стойка ещё не смотрела.</summary>
    private static async Task<Guid> RequestAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, new FixedTimeProvider(Now));
        var result = await service.CreateOnlineAsync(
            PlayerId, OrgId, BranchId,
            new CreatePlayerReservationRequest(SeatId, Start, Start.AddHours(1), null, TariffVersionId),
            CancellationToken.None);
        Assert.True(result.Succeeded);

        var reservation = await db.Reservations.SingleAsync(
            r => r.ReservationId == result.Response!.ReservationId);
        reservation.State = ReservationStateNames.Pending;
        reservation.ConfirmedAtUtc = null;
        reservation.RespondByUtc = Start;
        await db.SaveChangesAsync();
        return reservation.ReservationId;
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

    private static async Task SeedAsync(
        DbContextOptions<PlatformDbContext> options, bool keepPrepaymentOnNoShow = false)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Now });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Now });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000051", PreferredLocale = "ru", IsActive = true, CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 5_000, CurrencyCode = "TJS", CreatedAtUtc = Now.AddDays(-1)
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
        settings.KeepPrepaymentOnNoShow = keepPrepaymentOnNoShow;
        db.BranchBookingSettings.Add(settings);

        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
