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
        Assert.Equal(3800, summary.WalletBalance.MinorUnits);
        Assert.Equal(700, summary.DebtBalance.MinorUnits);
        Assert.Equal(3, summary.RecentEntries.Count);
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
        await db.SaveChangesAsync();

        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(db, packageId, CancellationToken.None);

        Assert.Equal(2700, remaining.IncludedSeconds);
        Assert.Equal(600, remaining.BonusSeconds);
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
        Guid? packageId = null)
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
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now
        };
    }
}
