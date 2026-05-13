using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class LedgerBalanceProjectorTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task GetWalletSummaryAsync_DerivesWalletDebtAndRecentEntriesFromLedgerRows()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -1200, 0));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700, 0));
        await db.SaveChangesAsync();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal("TJS", summary.WalletBalance.CurrencyCode);
        Assert.Equal("TJS", summary.DebtBalance.CurrencyCode);
        Assert.Equal(3800, summary.WalletBalance.MinorUnits);
        Assert.Equal(700, summary.DebtBalance.MinorUnits);
        Assert.Equal(3, summary.RecentEntries.Count);
    }

    [Fact]
    public async Task GetWalletSummaryAsync_ReturnsNullForUnknownPlayer()
    {
        await using var db = CreateDbContext();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetWalletSummaryAsync_ReturnsZeroBalancesInDefaultCurrencyForKnownPlayerWithNoEntries()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        await db.SaveChangesAsync();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal("TJS", summary.WalletBalance.CurrencyCode);
        Assert.Equal(0, summary.WalletBalance.MinorUnits);
        Assert.Equal("TJS", summary.DebtBalance.CurrencyCode);
        Assert.Equal(0, summary.DebtBalance.MinorUnits);
        Assert.Empty(summary.RecentEntries);
    }

    [Fact]
    public async Task GetWalletSummaryAsync_UsesSingleLedgerCurrencyForWalletAndDebtBalances()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0, currencyCode: "USD"));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700, 0, currencyCode: "USD"));
        await db.SaveChangesAsync();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal("USD", summary.WalletBalance.CurrencyCode);
        Assert.Equal("USD", summary.DebtBalance.CurrencyCode);
    }

    [Fact]
    public async Task GetWalletSummaryAsync_ThrowsForMixedCurrencyLedgerRows()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0, currencyCode: "TJS"));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700, 0, currencyCode: "USD"));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None));

        Assert.Contains("multiple currencies", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetWalletSummaryAsync_ReturnsRecentEntriesNewestFirstCappedAtTwentyFive()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);

        for (var index = 0; index < 30; index++)
        {
            db.LedgerEntries.Add(CreateEntry(
                LedgerEntryTypeNames.TopUp,
                LedgerAccountTypeNames.Wallet,
                index + 1,
                0,
                createdAtUtc: Now.AddMinutes(index)));
        }

        await db.SaveChangesAsync();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(25, summary.RecentEntries.Count);
        Assert.Equal(30, summary.RecentEntries[0].Amount.MinorUnits);
        Assert.Equal(6, summary.RecentEntries[^1].Amount.MinorUnits);
    }

    [Fact]
    public async Task GetPackageRemainingSecondsAsync_DerivesRemainingPackageAndBonusSeconds()
    {
        await using var db = CreateDbContext();
        var packageId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
        SeedPlayer(db);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 0, 3600, packageId));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.BonusGrant, LedgerAccountTypeNames.BonusTime, 0, 600, packageId));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PackageConsumption, LedgerAccountTypeNames.PackageTime, 0, -900, packageId));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.BonusConsumption, LedgerAccountTypeNames.BonusTime, 0, -200, packageId));
        await db.SaveChangesAsync();

        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(db, packageId, CancellationToken.None);

        Assert.Equal(2700, remaining.IncludedSeconds);
        Assert.Equal(400, remaining.BonusSeconds);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static void SeedPlayer(PlatformDbContext db)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = null,
            IsActive = true,
            CreatedAtUtc = Now
        });
    }

    private static LedgerEntryEntity CreateEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        Guid? packageId = null,
        string currencyCode = "TJS",
        DateTimeOffset? createdAtUtc = null)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            SessionId = null,
            PlayerPackageId = packageId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = currencyCode,
            Description = entryType,
            Reason = "test",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = createdAtUtc ?? Now
        };
    }
}
