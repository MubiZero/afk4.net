using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Бронь несёт выбор тарифа, сделанный игроком в приложении, и цену этого выбора считает сервер.
/// Это предусловие холда денег: замораживать нечего, пока бронь не знает, на какую сумму игрок
/// согласился.
/// </summary>
public sealed class EfReservationBillingChoiceTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid OtherBranchId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
    private static readonly Guid TariffId = Guid.Parse("11111111-1111-4111-1111-111111111111");
    private static readonly Guid TariffVersionId = Guid.Parse("22222222-2222-4222-2222-222222222222");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-18T16:00:00Z");

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task SeedAsync(
        DbContextOptions<PlatformDbContext> options,
        Guid tariffBranchId,
        DateTimeOffset? retiredAtUtc = null,
        int minimumBillableMinutes = 0,
        int roundingIncrementMinutes = 1)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Start });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Start });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000001", PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Start
        });
        // Кошелёк положительный — иначе бронь останется pending и тест мерил бы не то.
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
            EntryType = "topup", AccountType = LedgerAccountTypeNames.Wallet, AmountMinorUnits = 50_000,
            CurrencyCode = "TJS", CreatedAtUtc = Start
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = TariffId, OrganizationId = OrgId, BranchId = tariffBranchId,
            Name = "Ночной", IsActive = true, CreatedAtUtc = Start
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId, TariffId = TariffId, OrganizationId = OrgId, BranchId = tariffBranchId,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = minimumBillableMinutes, RoundingIncrementMinutes = roundingIncrementMinutes,
            EffectiveFromUtc = Start.AddYears(-1), RetiredAtUtc = retiredAtUtc
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ReservationServiceResult<ReservationDto>> BookAsync(
        DbContextOptions<PlatformDbContext> options,
        Guid? tariffVersionId,
        int hours = 1)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var request = new CreatePlayerReservationRequest(
            SeatId, Start, Start.AddHours(hours), "self-service", tariffVersionId);
        return await service.CreateOnlineAsync(PlayerId, OrgId, BranchId, request, CancellationToken.None);
    }

    [Fact]
    public async Task CreateOnlineAsync_PricesTheChosenTariff()
    {
        var options = NewOptions();
        await SeedAsync(options, tariffBranchId: BranchId);

        var result = await BookAsync(options, TariffVersionId);

        Assert.True(result.Succeeded);
        Assert.Equal(TariffVersionId, result.Response!.TariffVersionId);
        // 60 минут по 25 дирам — 1500, то есть 15,00 сомони.
        Assert.Equal(1_500, result.Response.EstimatedCostMinorUnits);
        Assert.Equal("TJS", result.Response.CurrencyCode);
        Assert.Equal("Ночной", result.Response.TariffName);
    }

    // Минимальное оплачиваемое время — ровно та причина, по которой цену считает сервер: клиент,
    // умножающий минуты на цену, показал бы другую сумму.
    [Fact]
    public async Task CreateOnlineAsync_AppliesMinimumBillableMinutes()
    {
        var options = NewOptions();
        await SeedAsync(options, tariffBranchId: BranchId, minimumBillableMinutes: 120);

        var result = await BookAsync(options, TariffVersionId);

        Assert.True(result.Succeeded);
        Assert.Equal(3_000, result.Response!.EstimatedCostMinorUnits);
    }

    [Fact]
    public async Task CreateOnlineAsync_RejectsTariffFromAnotherBranch()
    {
        var options = NewOptions();
        await SeedAsync(options, tariffBranchId: OtherBranchId);

        var result = await BookAsync(options, TariffVersionId);

        Assert.False(result.Succeeded);
        Assert.Contains("tariff", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // Снятая с публикации версия не заменяется «похожей»: игрок согласился на конкретную цену,
    // и тихо забронировать по другой — это враньё про деньги.
    [Fact]
    public async Task CreateOnlineAsync_RejectsRetiredTariffVersion()
    {
        var options = NewOptions();
        await SeedAsync(options, tariffBranchId: BranchId, retiredAtUtc: Start.AddDays(-1));

        var result = await BookAsync(options, TariffVersionId);

        Assert.False(result.Succeeded);
    }

    // Бронь без выбора тарифа остаётся законной: её создаёт оператор на стойке, и цену там
    // назначают при посадке.
    [Fact]
    public async Task CreateOnlineAsync_KeepsWorking_WithoutTariffChoice()
    {
        var options = NewOptions();
        await SeedAsync(options, tariffBranchId: BranchId);

        var result = await BookAsync(options, tariffVersionId: null);

        Assert.True(result.Succeeded);
        Assert.Null(result.Response!.TariffVersionId);
        Assert.Null(result.Response.EstimatedCostMinorUnits);
    }

    [Fact]
    public async Task CreateOnlineAsync_KeepsThePriceItStored_WhenTheTariffIsRetiredLater()
    {
        var options = NewOptions();
        await SeedAsync(options, tariffBranchId: BranchId);
        var created = await BookAsync(options, TariffVersionId);
        Assert.True(created.Succeeded);

        await using (var db = new PlatformDbContext(options))
        {
            var version = await db.TariffVersions.SingleAsync(v => v.TariffVersionId == TariffVersionId);
            version.RetiredAtUtc = Start;
            version.PricePerMinuteMinorUnits = 99;
            await db.SaveChangesAsync();
        }

        await using var read = new PlatformDbContext(options);
        var stored = await read.Reservations.AsNoTracking()
            .SingleAsync(r => r.ReservationId == created.Response!.ReservationId);

        // Цена лежит в самой брони, а не пересчитывается по сегодняшнему прайсу.
        Assert.Equal(1_500, stored.EstimatedCostMinorUnits);
        Assert.Equal("TJS", stored.CurrencyCode);
    }
}
