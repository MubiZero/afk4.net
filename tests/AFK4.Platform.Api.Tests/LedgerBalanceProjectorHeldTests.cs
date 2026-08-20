using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Третье число кошелька: сколько придержано под брони.
///
/// Главное свойство здесь — то, что НЕ меняется: остаток по-прежнему означает «сколько можно
/// потратить», и холд из него уже вычтен. «Придержано» ничего не переносит и ничего не пересчитывает,
/// оно только объясняет, куда делась часть остатка. Сделать остаток «валовым» соблазнительно и
/// ломает каждую проверку достаточности средств в проекте.
/// </summary>
public sealed class LedgerBalanceProjectorHeldTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task Held_SumsUnreleasedHolds_AndWalletStaysSpendable()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5_000));
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -1_500));
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -500));
        await db.SaveChangesAsync();

        var balances = await LedgerBalanceProjector.GetClubBalancesAsync(db, PlayerAccountId, CancellationToken.None);

        // Придержано показывается положительным числом: в журнале холд отрицателен, а человеку
        // говорят «придержано 20», а не «придержано минус 20».
        Assert.Equal(2_000, balances.HeldMinorUnits);
        Assert.Equal(3_000, balances.WalletMinorUnits);
    }

    [Fact]
    public async Task Held_BecomesZero_WhenTheHoldIsReleased()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5_000));
        var hold = Entry(LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -1_500);
        db.LedgerEntries.Add(hold);
        db.LedgerEntries.Add(Entry(
            LedgerEntryTypeNames.Reversal, LedgerAccountTypeNames.Wallet, 1_500, reversesLedgerEntryId: hold.LedgerEntryId));
        await db.SaveChangesAsync();

        var balances = await LedgerBalanceProjector.GetClubBalancesAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.Equal(0, balances.HeldMinorUnits);
        Assert.Equal(5_000, balances.WalletMinorUnits);
    }

    /// <summary>
    /// Удержание за неявку — не заморозка: деньги уже не придержаны, они клубу отданы. Спутать эти
    /// два состояния значит показать человеку «придержано» на то, чего у него больше нет.
    /// </summary>
    [Fact]
    public async Task Held_DoesNotCountTheNoShowFee()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5_000));
        var hold = Entry(LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -1_500);
        db.LedgerEntries.Add(hold);
        db.LedgerEntries.Add(Entry(
            LedgerEntryTypeNames.Reversal, LedgerAccountTypeNames.Wallet, 1_500, reversesLedgerEntryId: hold.LedgerEntryId));
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.ReservationNoShowFee, LedgerAccountTypeNames.Wallet, -1_500));
        await db.SaveChangesAsync();

        var balances = await LedgerBalanceProjector.GetClubBalancesAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.Equal(0, balances.HeldMinorUnits);
        Assert.Equal(3_500, balances.WalletMinorUnits);
    }

    [Fact]
    public async Task WalletSummary_CarriesTheThirdNumber_WithoutMovingTheFirstTwo()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db);
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5_000));
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -1_500));
        db.LedgerEntries.Add(Entry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700));
        await db.SaveChangesAsync();

        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(db, PlayerAccountId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(3_500, summary.WalletBalance.MinorUnits);
        Assert.Equal(1_500, summary.HeldBalance.MinorUnits);
        Assert.Equal(700, summary.DebtBalance.MinorUnits);
        Assert.Equal("TJS", summary.HeldBalance.CurrencyCode);
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static void SeedPlayer(PlatformDbContext db)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            IsActive = true,
            CreatedAtUtc = Now
        });
    }

    private static LedgerEntryEntity Entry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        Guid? reversesLedgerEntryId = null) =>
        new()
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test",
            ReversesLedgerEntryId = reversesLedgerEntryId,
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now
        };
}
