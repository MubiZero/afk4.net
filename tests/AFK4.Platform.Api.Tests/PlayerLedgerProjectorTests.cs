using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerLedgerProjectorTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherPlayerAccountId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    // Статическая база времени — никаких date-бомб (Global Constraints).
    private static readonly DateTimeOffset Base = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task GetLedgerPageAsync_ReturnsNewestFirst_AndCapsAtLimitWithNextCursor()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        // 5 записей, по минуте друг от друга; newest = index 4.
        for (var i = 0; i < 5; i++)
        {
            db.LedgerEntries.Add(CreateEntry(
                LedgerEntryTypeNames.TopUp,
                LedgerAccountTypeNames.Wallet,
                (i + 1) * 1000,
                createdAtUtc: Base.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, entryType: null, accountType: null, before: null, limit: 2, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        // newest first: amount 5000 (index 4), затем 4000 (index 3).
        Assert.Equal(5000, page.Items[0].Amount.MinorUnits);
        Assert.Equal(4000, page.Items[1].Amount.MinorUnits);
        Assert.NotNull(page.NextCursor); // есть ещё страницы
    }

    [Fact]
    public async Task GetLedgerPageAsync_SecondPageByCursor_DoesNotOverlapFirst_AndExhaustsCleanly()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        for (var i = 0; i < 5; i++)
        {
            db.LedgerEntries.Add(CreateEntry(
                LedgerEntryTypeNames.TopUp,
                LedgerAccountTypeNames.Wallet,
                (i + 1) * 1000,
                createdAtUtc: Base.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var first = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: null, limit: 2, CancellationToken.None);
        var second = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: first.NextCursor, limit: 2, CancellationToken.None);

        // вторая страница: amount 3000 (index 2), 2000 (index 1) — не пересекается с [5000, 4000].
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(3000, second.Items[0].Amount.MinorUnits);
        Assert.Equal(2000, second.Items[1].Amount.MinorUnits);
        Assert.NotNull(second.NextCursor);

        var firstIds = first.Items.Select(e => e.LedgerEntryId).ToHashSet();
        Assert.DoesNotContain(second.Items[0].LedgerEntryId, firstIds);
        Assert.DoesNotContain(second.Items[1].LedgerEntryId, firstIds);

        var third = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: second.NextCursor, limit: 2, CancellationToken.None);
        // осталась одна запись (amount 1000, index 0) — последняя страница, курсора больше нет.
        Assert.Single(third.Items);
        Assert.Equal(1000, third.Items[0].Amount.MinorUnits);
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task GetLedgerPageAsync_FiltersByEntryType()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -1200, createdAtUtc: Base.AddMinutes(2)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 3000, createdAtUtc: Base.AddMinutes(3)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, entryType: LedgerEntryTypeNames.TopUp, accountType: null, before: null, limit: 50, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, e => Assert.Equal(LedgerEntryTypeNames.TopUp, e.EntryType));
    }

    [Fact]
    public async Task GetLedgerPageAsync_FiltersByAccountType()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700, createdAtUtc: Base.AddMinutes(2)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, entryType: null, accountType: LedgerAccountTypeNames.Debt, before: null, limit: 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(LedgerAccountTypeNames.Debt, page.Items[0].AccountType);
    }

    [Fact]
    public async Task GetLedgerPageAsync_ScopesToSinglePlayer()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        SeedPlayer(db, OtherPlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 9000, playerAccountId: OtherPlayerAccountId, createdAtUtc: Base.AddMinutes(2)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: null, limit: 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(5000, page.Items[0].Amount.MinorUnits);
    }

    [Fact]
    public async Task GetLedgerPageAsync_BadCursor_FallsBackToFirstPage()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: "not-a-valid-cursor", limit: 50, CancellationToken.None);

        Assert.Single(page.Items); // битый курсор → первая страница, не падаем
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(1000, 100)]
    public void ClampLimit_BoundsToOneHundred(int? input, int expected)
    {
        Assert.Equal(expected, PlayerLedgerFilter.ClampLimit(input));
    }

    [Fact]
    public void IsValidEntryType_AcceptsKnownAndNull_RejectsUnknown()
    {
        Assert.True(PlayerLedgerFilter.IsValidEntryType(null));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(""));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(LedgerEntryTypeNames.TopUp));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(LedgerEntryTypeNames.Refund));
        // Заморозка под бронь и удержание за неявку — обычные строки выписки, и отфильтровать
        // их человек вправе так же, как пополнения: иначе эндпоинт отвечает 400 на собственный тип.
        Assert.True(PlayerLedgerFilter.IsValidEntryType(LedgerEntryTypeNames.ReservationHold));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(LedgerEntryTypeNames.ReservationNoShowFee));
        Assert.False(PlayerLedgerFilter.IsValidEntryType("mystery_type"));
    }

    [Fact]
    public void IsValidAccountType_AcceptsKnownAndNull_RejectsUnknown()
    {
        Assert.True(PlayerLedgerFilter.IsValidAccountType(null));
        Assert.True(PlayerLedgerFilter.IsValidAccountType(""));
        Assert.True(PlayerLedgerFilter.IsValidAccountType(LedgerAccountTypeNames.Wallet));
        Assert.True(PlayerLedgerFilter.IsValidAccountType(LedgerAccountTypeNames.Debt));
        Assert.False(PlayerLedgerFilter.IsValidAccountType("mystery_account"));
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static void SeedPlayer(PlatformDbContext db, Guid playerAccountId)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player",
            PhoneNumber = null,
            IsActive = true,
            CreatedAtUtc = Base
        });
    }

    private static LedgerEntryEntity CreateEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        Guid? playerAccountId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = playerAccountId ?? PlayerAccountId,
            SessionId = null,
            PlayerPackageId = null,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = createdAtUtc ?? Base
        };
    }
}
