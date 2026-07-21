using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Loyalty;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class LoyaltyAccrualServiceTests
{
    private static readonly Guid OrgId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid PlayerId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-09T10:00:00Z");

    private static PlatformDbContext Db() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static async Task SeedSettingsAsync(
        PlatformDbContext db, bool topUpEnabled, int topUpBps, bool shopEnabled, int shopBps,
        bool sessionEnabled = false, int sessionBps = 0, long capMinorUnits = 0, long minimumSourceMinorUnits = 0)
    {
        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = OrgId, TopUpEnabled = topUpEnabled, TopUpPercentBasisPoints = topUpBps,
            ShopEnabled = shopEnabled, ShopPercentBasisPoints = shopBps,
            SessionEnabled = sessionEnabled, SessionPercentBasisPoints = sessionBps,
            CashbackCapMinorUnits = capMinorUnits, MinimumSourceMinorUnits = minimumSourceMinorUnits,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task BuildsSessionCashbackWhenEnabled()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: false, topUpBps: 0, shopEnabled: false, shopBps: 0,
            sessionEnabled: true, sessionBps: 1000);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.Session, OrgId, BranchId, PlayerId, sessionId: Guid.NewGuid(),
            sourceMinorUnits: 2000, currencyCode: "TJS", reason: "cashback:session", Now, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryTypeNames.Cashback, entry!.EntryType);
        Assert.Equal(200, entry.AmountMinorUnits); // floor(2000 * 1000 / 10000) = 200
    }

    [Fact]
    public async Task ClampsCashbackToCap()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: true, topUpBps: 1000, shopEnabled: false, shopBps: 0,
            capMinorUnits: 150);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, OrgId, BranchId, PlayerId, null,
            sourceMinorUnits: 5000, currencyCode: "TJS", reason: "cashback:topup", Now, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(150, entry!.AmountMinorUnits); // floor(5000 * 1000 / 10000) = 500 clamped to cap 150
    }

    [Fact]
    public async Task ReturnsNullWhenSourceBelowMinimum()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: true, topUpBps: 1000, shopEnabled: false, shopBps: 0,
            minimumSourceMinorUnits: 1000);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, OrgId, BranchId, PlayerId, null,
            sourceMinorUnits: 999, currencyCode: "TJS", reason: "cashback:topup", Now, CancellationToken.None);

        Assert.Null(entry); // 999 < minimum 1000 -> no cashback
    }

    [Fact]
    public async Task BuildsFlooredCashbackForEnabledTopUp()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: true, topUpBps: 500, shopEnabled: false, shopBps: 0);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, OrgId, BranchId, PlayerId, sessionId: null,
            sourceMinorUnits: 999, currencyCode: "TJS", reason: "cashback:topup", Now, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryTypeNames.Cashback, entry!.EntryType);
        Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
        Assert.Equal(49, entry.AmountMinorUnits); // floor(999 * 500 / 10000) = 49
        Assert.Equal("TJS", entry.CurrencyCode);
        Assert.Equal(Guid.Empty, entry.CreatedByStaffUserId);
    }

    [Fact]
    public async Task ReturnsNullWhenSourceDisabled()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: false, topUpBps: 500, shopEnabled: true, shopBps: 300);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, OrgId, BranchId, PlayerId, null, 10000, "TJS", "cashback:topup", Now, CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReturnsNullWhenNoSettingsRow()
    {
        await using var db = Db();
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.Shop, OrgId, BranchId, PlayerId, null, 10000, "TJS", "cashback:shop", Now, CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReturnsNullWhenComputedCashbackRoundsToZero()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: false, topUpBps: 0, shopEnabled: true, shopBps: 100);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.Shop, OrgId, BranchId, PlayerId, null, 50, "TJS", "cashback:shop", Now, CancellationToken.None);

        Assert.Null(entry); // floor(50 * 100 / 10000) = 0 -> no entry
    }
}
