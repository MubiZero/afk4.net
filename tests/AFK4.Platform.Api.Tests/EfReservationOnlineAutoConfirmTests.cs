using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfReservationOnlineAutoConfirmTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-18T16:00:00Z");

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static LedgerEntryEntity Ledger(string accountType, long amount) => new()
    {
        LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = PlayerId,
        EntryType = "topup", AccountType = accountType, AmountMinorUnits = amount, CurrencyCode = "TJS", CreatedAtUtc = Start
    };

    private static async Task SeedAsync(DbContextOptions<PlatformDbContext> options, long walletMinor, long debtMinor)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Start });
        db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Start });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId, OrganizationId = OrgId, HomeBranchId = BranchId, DisplayName = "Игрок",
            PhoneNumber = "+992900000001", PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Start
        });
        if (walletMinor != 0) db.LedgerEntries.Add(Ledger(LedgerAccountTypeNames.Wallet, walletMinor));
        if (debtMinor != 0) db.LedgerEntries.Add(Ledger(LedgerAccountTypeNames.Debt, debtMinor));
        await db.SaveChangesAsync();
    }

    private static async Task<string> CreateOnlineStateAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var request = new CreatePlayerReservationRequest(SeatId, Start, Start.AddHours(1), "self-service");
        var result = await service.CreateOnlineAsync(PlayerId, OrgId, BranchId, request, CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Response!.State;
    }

    [Fact]
    public async Task CreateOnlineAsync_AutoConfirms_WhenWalletIsPositiveAndNoDebt()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000, debtMinor: 0);
        Assert.Equal(ReservationStateNames.Confirmed, await CreateOnlineStateAsync(options));
    }

    [Fact]
    public async Task CreateOnlineAsync_StaysPending_WhenWalletIsEmpty()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 0, debtMinor: 0);
        Assert.Equal(ReservationStateNames.Pending, await CreateOnlineStateAsync(options));
    }

    [Fact]
    public async Task CreateOnlineAsync_StaysPending_WhenPlayerInDebt()
    {
        var options = NewOptions();
        await SeedAsync(options, walletMinor: 5_000, debtMinor: 3_000);
        Assert.Equal(ReservationStateNames.Pending, await CreateOnlineStateAsync(options));
    }
}
