using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Тариф с расписанием нельзя выбрать вне его часов.
///
/// Дешёвое утро продаётся отдельным тарифом, а не окнами внутри одного: сессия считается одной
/// ставкой на всю длительность. Значит и вся механика сводится к запрету выбора — утренний тариф
/// просто не продаётся вечером, а расчёт денег не меняется вовсе.
///
/// Часовой пояс филиала здесь UTC: проверяется запрет, а не перевод времени — его отдельно
/// разбирают модульные тесты расписания. Один тест берёт настоящий Душанбе, чтобы смещение точно
/// доезжало до базы, а не терялось по дороге.
/// </summary>
public class TariffScheduleGateTests
{
    // Понедельник.
    private static readonly DateTimeOffset MondayMorning = DateTimeOffset.Parse("2026-08-17T10:00:00Z");
    private static readonly DateTimeOffset MondayEvening = DateTimeOffset.Parse("2026-08-17T20:00:00Z");

    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private const int EightAm = 8 * 60;
    private const int FourPm = 16 * 60;

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedMorningTariffAsync(
        PlatformDbContext db,
        string timeZone = "UTC",
        int daysMask = TariffSchedule.EveryDayMask,
        int? fromMinuteOfDay = EightAm,
        int? toMinuteOfDay = FourPm)
    {
        var tariffId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId, Name = "Schedule Org", CreatedAtUtc = MondayMorning
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Slug = "sched",
            Name = "CyberX", City = "Душанбе", PreferredTimeZone = timeZone, CreatedAtUtc = MondayMorning
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId, OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId, DisplayName = "Игрок", PhoneNumber = "+992900000001",
            PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = MondayMorning
        });
        // Бронь с тарифом сразу замораживает деньги, поэтому кошелёк должен быть не пуст: иначе
        // «нет денег» подменит собой проверку расписания, которую этот файл и разбирает.
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId, PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 50_000, CurrencyCode = "TJS", CreatedAtUtc = MondayMorning
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
            Name = "Утренний", IsActive = true, AppliesOnDaysMask = daysMask,
            AppliesFromMinuteOfDay = fromMinuteOfDay, AppliesToMinuteOfDay = toMinuteOfDay,
            CreatedAtUtc = MondayMorning
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = versionId, TariffId = tariffId, OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId, VersionNumber = 1, CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 10, MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1,
            EffectiveFromUtc = MondayMorning.AddYears(-1)
        });
        await db.SaveChangesAsync();
        return versionId;
    }

    private static SessionBillingService CreateBilling(PlatformDbContext db, DateTimeOffset now)
    {
        var timeProvider = new FixedTimeProvider(now);
        return new SessionBillingService(
            db,
            new EfTariffService(db, timeProvider),
            new AlwaysOpenShiftResolver(),
            new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance),
            timeProvider);
    }

    private static Task<SessionBillingValidationResult> ValidateStartAsync(
        PlatformDbContext db, Guid versionId, DateTimeOffset now) =>
        CreateBilling(db, now).ValidateStartAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            PlayerAccountId,
            BillingModeNames.PostpaidDebt,
            versionId,
            playerPackageId: null,
            durationMinutes: 60,
            CancellationToken.None);

    [Fact]
    public async Task AMorningTariff_CannotBeSoldInTheEvening()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);

        var result = await ValidateStartAsync(db, versionId, MondayEvening);

        Assert.False(result.Succeeded);
        Assert.Equal(TariffSchedule.OutsideHoursCode, result.Error);
    }

    [Fact]
    public async Task AMorningTariff_SellsInsideItsOwnHours()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);

        var result = await ValidateStartAsync(db, versionId, MondayMorning);

        Assert.True(result.Succeeded);
        Assert.Equal(600, result.AmountMinorUnits);
    }

    // Тариф без расписания — это все существующие тарифы клубов: они обязаны работать как прежде.
    [Fact]
    public async Task ATariffWithoutASchedule_SellsAtAnyHour()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(
            db, fromMinuteOfDay: null, toMinuteOfDay: null);

        var result = await ValidateStartAsync(db, versionId, MondayEvening);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task AWeekdayTariff_CannotBeSoldOnASaturday()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(
            db,
            daysMask: 0b000_1111 | (1 << 4),
            fromMinuteOfDay: null,
            toMinuteOfDay: null);

        var saturday = DateTimeOffset.Parse("2026-08-22T10:00:00Z");
        Assert.Equal(TariffSchedule.OutsideHoursCode, (await ValidateStartAsync(db, versionId, saturday)).Error);
        Assert.True((await ValidateStartAsync(db, versionId, MondayMorning)).Succeeded);
    }

    /// <summary>
    /// Смещение филиала должно доезжать до запрета, а не теряться по дороге: 05:00 UTC — это
    /// десять утра в Душанбе, и утренний тариф там действует.
    /// </summary>
    [Fact]
    public async Task TheBranchTimeZoneDecidesWhatCountsAsMorning()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db, timeZone: "Asia/Dushanbe");

        var fiveAmUtc = DateTimeOffset.Parse("2026-08-17T05:00:00Z");
        Assert.True((await ValidateStartAsync(db, versionId, fiveAmUtc)).Succeeded);

        // 20:00 в Душанбе — это 15:00 UTC; утренний тариф там уже кончился.
        var threePmUtc = DateTimeOffset.Parse("2026-08-17T15:00:00Z");
        Assert.Equal(
            TariffSchedule.OutsideHoursCode, (await ValidateStartAsync(db, versionId, threePmUtc)).Error);
    }

    /// <summary>
    /// Бронь смотрит на своё время начала, а не на «сейчас». Игрок в восемь вечера бронирует
    /// завтрашнее утро — утренний тариф для этой брони действует, хотя в момент нажатия нет.
    /// </summary>
    [Fact]
    public async Task ABooking_IsCheckedAgainstItsOwnStartAndNotAgainstNow()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);
        var service = new EfReservationService(db, new FixedTimeProvider(MondayEvening));

        var tomorrowMorning = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        var result = await service.CreateOnlineAsync(
            PlayerAccountId,
            TestIds.OrganizationId,
            TestIds.BranchId,
            new CreatePlayerReservationRequest(
                null, tomorrowMorning, tomorrowMorning.AddHours(1), null, versionId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(600, result.Response!.EstimatedCostMinorUnits);
    }

    [Fact]
    public async Task ABookingForAnHourTheTariffDoesNotCover_IsRefusedByName()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);
        var service = new EfReservationService(db, new FixedTimeProvider(MondayMorning));

        var tomorrowEvening = DateTimeOffset.Parse("2026-08-18T20:00:00Z");
        var result = await service.CreateOnlineAsync(
            PlayerAccountId,
            TestIds.OrganizationId,
            TestIds.BranchId,
            new CreatePlayerReservationRequest(
                null, tomorrowEvening, tomorrowEvening.AddHours(1), null, versionId),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(TariffSchedule.OutsideHoursCode, result.Error);
    }

    // Список тарифов не прячет то, что сейчас не действует: пропавший «Утренний» читается как
    // сбой приложения, а названный со своими часами объясняет и себя, и свою недоступность.
    [Fact]
    public async Task TheTariffList_ShowsAnOutOfHoursTariffAndMarksItUnavailable()
    {
        await using var db = CreateDbContext();
        await SeedMorningTariffAsync(db);
        var reference = new EfOperatorReferenceDataService(db, new FixedTimeProvider(MondayEvening));

        var options = await reference.GetTariffOptionsAsync(
            TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        var morning = Assert.Single(options);
        Assert.Equal("Утренний", morning.Name);
        Assert.False(morning.AppliesNow);
        Assert.Equal(EightAm, morning.AppliesFromMinuteOfDay);
        Assert.Equal(FourPm, morning.AppliesToMinuteOfDay);
    }

    [Fact]
    public async Task TheTariffList_MarksATariffAvailableInsideItsHours()
    {
        await using var db = CreateDbContext();
        await SeedMorningTariffAsync(db);
        var reference = new EfOperatorReferenceDataService(db, new FixedTimeProvider(MondayMorning));

        var options = await reference.GetTariffOptionsAsync(
            TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        Assert.True(Assert.Single(options).AppliesNow);
    }

    private sealed class AlwaysOpenShiftResolver : IOpenShiftResolver
    {
        public Task<BillingCommandServiceResult<Guid>> GetOpenShiftIdAsync(
            Guid organizationId,
            Guid branchId,
            CancellationToken cancellationToken) =>
            Task.FromResult(BillingCommandServiceResult<Guid>.Ok(Guid.NewGuid()));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    /// <summary>
    /// Дыра, найденная ревью: проверялось только начало. Бронь с 08:00 до 23:00 на утреннем
    /// тарифе проходила целиком по утренней цене — пятнадцать часов, из них семь вечерних.
    /// </summary>
    [Fact]
    public async Task ABookingThatRunsPastTheWindow_IsRefused()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);
        var service = new EfReservationService(db, new FixedTimeProvider(MondayEvening));

        var tomorrowMorning = DateTimeOffset.Parse("2026-08-18T08:00:00Z");
        var result = await service.CreateOnlineAsync(
            PlayerAccountId,
            TestIds.OrganizationId,
            TestIds.BranchId,
            new CreatePlayerReservationRequest(
                null, tomorrowMorning, DateTimeOffset.Parse("2026-08-18T23:00:00Z"), null, versionId),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(TariffSchedule.OutsideHoursCode, result.Error);
    }

    // Бронь, целиком укладывающаяся в окно, по-прежнему проходит: запрет не должен закрыть то,
    // ради чего тариф и заводят.
    [Fact]
    public async Task ABookingWhollyInsideTheWindow_StillGoesThrough()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);
        var service = new EfReservationService(db, new FixedTimeProvider(MondayEvening));

        var from = DateTimeOffset.Parse("2026-08-18T09:00:00Z");
        var result = await service.CreateOnlineAsync(
            PlayerAccountId,
            TestIds.OrganizationId,
            TestIds.BranchId,
            new CreatePlayerReservationRequest(null, from, from.AddHours(2), null, versionId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Та же дыра со стороны сессии: старт в 15:30 на двенадцать часов проходил по утренней
    /// ставке — одиннадцать с половиной из них вечерние.
    /// </summary>
    [Fact]
    public async Task ASessionLongerThanTheWindow_IsRefusedAtStart()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);

        var halfPastThree = DateTimeOffset.Parse("2026-08-17T15:30:00Z");
        var result = await CreateBilling(db, halfPastThree).ValidateStartAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            PlayerAccountId,
            BillingModeNames.PostpaidDebt,
            versionId,
            playerPackageId: null,
            durationMinutes: 720,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(TariffSchedule.OutsideHoursCode, result.Error);
    }

    [Fact]
    public async Task ASessionThatFitsInsideTheWindow_StillStarts()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);

        var result = await ValidateStartAsync(db, versionId, MondayMorning);

        Assert.True(result.Succeeded);
    }
    /// <summary>
    /// Найдено ревью: поля расписания в запросе имели значения по умолчанию и присваивались
    /// безусловно, поэтому вызов, не знающий про часы, стирал их молча — тариф возвращался из
    /// архива круглосуточным и продавал утро по вечерней цене.
    /// </summary>
    [Fact]
    public async Task UpdatingATariffWithoutMentioningHours_KeepsThem()
    {
        await using var db = CreateDbContext();
        await SeedMorningTariffAsync(db);
        var tariff = db.Tariffs.Single();
        var service = new EfTariffService(db, new FixedTimeProvider(MondayMorning));

        var result = await service.UpdateTariffAsync(
            TestIds.BranchId,
            tariff.TariffId,
            Guid.NewGuid(),
            new UpdateTariffRequest(TestIds.OrganizationId, "Утренний", IsActive: false),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var stored = db.Tariffs.Single();
        Assert.Equal(EightAm, stored.AppliesFromMinuteOfDay);
        Assert.Equal(FourPm, stored.AppliesToMinuteOfDay);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task UpdatingATariffWithAScheduleReplacesIt()
    {
        await using var db = CreateDbContext();
        await SeedMorningTariffAsync(db);
        var tariff = db.Tariffs.Single();
        var service = new EfTariffService(db, new FixedTimeProvider(MondayMorning));

        var result = await service.UpdateTariffAsync(
            TestIds.BranchId,
            tariff.TariffId,
            Guid.NewGuid(),
            new UpdateTariffRequest(
                TestIds.OrganizationId, "Утренний", IsActive: true, new TariffScheduleDto(0, 22 * 60, 6 * 60)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var stored = db.Tariffs.Single();
        Assert.Equal(22 * 60, stored.AppliesFromMinuteOfDay);
        Assert.Equal(6 * 60, stored.AppliesToMinuteOfDay);
    }

    /// <summary>
    /// Найдено ревью: стоимость бесплатного времени идёт в порог одобрения менеджером, и утренний
    /// тариф вечером занижал бы её.
    /// </summary>
    [Fact]
    public async Task ACompSessionCannotBeValuedByAnOutOfHoursTariff()
    {
        await using var db = CreateDbContext();
        var versionId = await SeedMorningTariffAsync(db);

        var result = await CreateBilling(db, MondayEvening).ComputeCompValueAsync(
            TestIds.OrganizationId, TestIds.BranchId, versionId, 600, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(TariffSchedule.OutsideHoursCode, result.Error);
    }
}
