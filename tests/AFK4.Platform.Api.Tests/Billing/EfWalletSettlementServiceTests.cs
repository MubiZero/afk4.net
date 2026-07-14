using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfWalletSettlementServiceTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid PlayerAccountId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ShiftId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid StaffUserId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid OrderId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-13T10:00:00Z");

    [Fact]
    public async Task DebitAsync_StagesWalletPaymentWithShiftAndReference()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            SessionId,
            ShiftId,
            1_500,
            "tjs",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        var entry = Assert.IsType<LedgerEntryEntity>(result.Entry);
        Assert.Equal(-1_500, entry.AmountMinorUnits);
        Assert.Equal(ShiftId, entry.ShiftId);
        Assert.Equal(SessionId, entry.SessionId);
        Assert.Equal(LedgerEntryTypeNames.WalletPayment, entry.EntryType);
        Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
        Assert.Equal("TJS", entry.CurrencyCode);
        Assert.Equal("shop_order", entry.Description);
        Assert.Equal(OrderId.ToString("D"), entry.Reason);
        Assert.Equal(EntityState.Added, db.Entry(entry).State);
        Assert.Equal(1, await db.LedgerEntries.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task DebitAsync_InsufficientBalance_StagesNothing()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            SessionId,
            ShiftId,
            1_501,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("insufficient_funds", result.ErrorCode);
        Assert.Null(result.Entry);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task DebitAsync_ScopesBalanceToOrganizationBranchPlayerAndWallet()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        db.LedgerEntries.AddRange(
            CreateLedgerEntry(OrganizationId, Guid.NewGuid(), PlayerAccountId, 50_000, "TJS"),
            CreateLedgerEntry(Guid.NewGuid(), BranchId, PlayerAccountId, 50_000, "TJS"),
            CreateLedgerEntry(OrganizationId, BranchId, Guid.NewGuid(), 50_000, "TJS"),
            CreateLedgerEntry(OrganizationId, BranchId, PlayerAccountId, 50_000, "TJS", LedgerAccountTypeNames.Debt));
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            1_501,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.Equal("insufficient_funds", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task DebitAsync_WrongPlayerScope_IsRejected()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            Guid.NewGuid(),
            PlayerAccountId,
            null,
            ShiftId,
            1_000,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.Equal("player_not_found", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task DebitAsync_MismatchedCurrency_IsRejectedWithoutStaging()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            1_000,
            "USD",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.Equal("currency_mismatch", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task DebitAsync_CurrencyComparisonIsCaseInsensitiveAcrossLedgerEntries()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_000);
        db.LedgerEntries.Add(CreateLedgerEntry(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            500,
            "tjs"));
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            1_500,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("TJS", result.Entry!.CurrencyCode);
    }

    [Fact]
    public async Task DebitAsync_IgnoresNonWalletCurrenciesWhenValidatingWalletLedger()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        db.LedgerEntries.Add(CreateLedgerEntry(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            5_000,
            "USD",
            LedgerAccountTypeNames.Debt));
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            1_500,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task DebitAsync_RepeatedBeforeSave_IncludesStagedDebitInBalance()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_000);
        var service = new EfWalletSettlementService(db);

        var first = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            800,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);
        var second = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            800,
            "TJS",
            "shop_order",
            Guid.NewGuid().ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.Equal("insufficient_funds", second.ErrorCode);
        Assert.Single(
            db.ChangeTracker.Entries<LedgerEntryEntity>(),
            entry => entry.State == EntityState.Added && entry.Entity.EntryType == LedgerEntryTypeNames.WalletPayment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DebitAsync_NonPositiveAmount_IsRejected(long amountMinorUnits)
    {
        await using var db = CreateDbContext();
        await SeedPlayerAndBalanceAsync(db, balanceMinorUnits: 1_500);
        var service = new EfWalletSettlementService(db);

        var result = await service.DebitAsync(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            ShiftId,
            amountMinorUnits,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            StaffUserId,
            Now,
            CancellationToken.None);

        Assert.Equal("invalid_amount", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task ReverseAsync_StagesOneReversalOfOriginalDebit()
    {
        await using var db = CreateDbContext();
        var originalDebit = CreateWalletDebit();
        db.LedgerEntries.Add(originalDebit);
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var reversal = Assert.IsType<LedgerEntryEntity>(result.Entry);
        Assert.Equal(originalDebit.LedgerEntryId, reversal.ReversesLedgerEntryId);
        Assert.Equal(1_500, reversal.AmountMinorUnits);
        Assert.Equal(LedgerEntryTypeNames.Reversal, reversal.EntryType);
        Assert.Equal(originalDebit.OrganizationId, reversal.OrganizationId);
        Assert.Equal(originalDebit.BranchId, reversal.BranchId);
        Assert.Equal(originalDebit.PlayerAccountId, reversal.PlayerAccountId);
        Assert.Equal(originalDebit.SessionId, reversal.SessionId);
        Assert.Equal(originalDebit.ShiftId, reversal.ShiftId);
        Assert.Equal(originalDebit.CurrencyCode, reversal.CurrencyCode);
        Assert.Equal(EntityState.Added, db.Entry(reversal).State);
        Assert.Equal(1, await db.LedgerEntries.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ReverseAsync_FabricatedLedgerEntryId_IsRejected()
    {
        await using var db = CreateDbContext();
        var fabricatedDebit = CreateWalletDebit();
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            fabricatedDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.Equal("original_debit_not_found", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task ReverseAsync_AddedOnlyOriginal_IsRejected()
    {
        await using var db = CreateDbContext();
        var addedOnlyDebit = CreateWalletDebit();
        db.LedgerEntries.Add(addedOnlyDebit);
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            addedOnlyDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.Equal("original_debit_not_found", result.ErrorCode);
        Assert.DoesNotContain(
            db.ChangeTracker.Entries<LedgerEntryEntity>(),
            entry => entry.State == EntityState.Added && entry.Entity.EntryType == LedgerEntryTypeNames.Reversal);
    }

    [Fact]
    public async Task ReverseAsync_DetachedMutatedInput_UsesCanonicalPersistedDebit()
    {
        await using var db = CreateDbContext();
        var originalDebit = CreateWalletDebit();
        db.LedgerEntries.Add(originalDebit);
        await db.SaveChangesAsync();
        db.Entry(originalDebit).State = EntityState.Detached;
        originalDebit.OrganizationId = Guid.NewGuid();
        originalDebit.BranchId = Guid.NewGuid();
        originalDebit.PlayerAccountId = Guid.NewGuid();
        originalDebit.SessionId = Guid.NewGuid();
        originalDebit.ShiftId = Guid.NewGuid();
        originalDebit.AmountMinorUnits = -999_999;
        originalDebit.CurrencyCode = "USD";
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var reversal = Assert.IsType<LedgerEntryEntity>(result.Entry);
        Assert.Equal(1_500, reversal.AmountMinorUnits);
        Assert.Equal(OrganizationId, reversal.OrganizationId);
        Assert.Equal(BranchId, reversal.BranchId);
        Assert.Equal(PlayerAccountId, reversal.PlayerAccountId);
        Assert.Equal(SessionId, reversal.SessionId);
        Assert.Equal(ShiftId, reversal.ShiftId);
        Assert.Equal("TJS", reversal.CurrencyCode);
    }

    [Fact]
    public async Task ReverseAsync_PersistedLongMinValueDebit_IsRejected()
    {
        await using var db = CreateDbContext();
        var originalDebit = CreateWalletDebit(amountMinorUnits: long.MinValue);
        db.LedgerEntries.Add(originalDebit);
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.Equal("invalid_original_debit", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task ReverseAsync_PersistedDebitWithQuantity_IsRejected()
    {
        await using var db = CreateDbContext();
        var originalDebit = CreateWalletDebit(quantitySeconds: 60);
        db.LedgerEntries.Add(originalDebit);
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.Equal("invalid_original_debit", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task ReverseAsync_WhenPersistedReversalExists_RejectsDuplicate()
    {
        await using var db = CreateDbContext();
        var originalDebit = CreateWalletDebit();
        db.LedgerEntries.AddRange(originalDebit, CreateReversal(originalDebit));
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var result = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.Equal("already_reversed", result.ErrorCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task ReverseAsync_CalledTwiceBeforeSave_StagesOnlyOneReversal()
    {
        await using var db = CreateDbContext();
        var originalDebit = CreateWalletDebit();
        db.LedgerEntries.Add(originalDebit);
        await db.SaveChangesAsync();
        var service = new EfWalletSettlementService(db);

        var first = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);
        var second = await service.ReverseAsync(
            originalDebit,
            StaffUserId,
            "shop_order_cancel",
            OrderId.ToString("D"),
            Now,
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.Equal("already_reversed", second.ErrorCode);
        Assert.Single(db.ChangeTracker.Entries<LedgerEntryEntity>(), entry => entry.State == EntityState.Added);
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task SeedPlayerAndBalanceAsync(PlatformDbContext db, long balanceMinorUnits)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = OrganizationId,
            HomeBranchId = BranchId,
            DisplayName = "Settlement Player",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(CreateLedgerEntry(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            balanceMinorUnits,
            "TJS"));
        await db.SaveChangesAsync();
    }

    private static LedgerEntryEntity CreateWalletDebit(long amountMinorUnits = -1_500, int quantitySeconds = 0) =>
        BillingEntryFactory.Create(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            SessionId,
            playerPackageId: null,
            LedgerEntryTypeNames.WalletPayment,
            LedgerAccountTypeNames.Wallet,
            amountMinorUnits,
            quantitySeconds,
            "TJS",
            "shop_order",
            OrderId.ToString("D"),
            reversesLedgerEntryId: null,
            StaffUserId,
            Now,
            ShiftId);

    private static LedgerEntryEntity CreateReversal(LedgerEntryEntity originalDebit) =>
        BillingEntryFactory.Create(
            originalDebit.OrganizationId,
            originalDebit.BranchId,
            originalDebit.PlayerAccountId,
            originalDebit.SessionId,
            originalDebit.PlayerPackageId,
            LedgerEntryTypeNames.Reversal,
            originalDebit.AccountType,
            -originalDebit.AmountMinorUnits,
            -originalDebit.QuantitySeconds,
            originalDebit.CurrencyCode,
            "shop_order_cancel",
            OrderId.ToString("D"),
            originalDebit.LedgerEntryId,
            StaffUserId,
            Now,
            originalDebit.ShiftId);

    private static LedgerEntryEntity CreateLedgerEntry(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        long amountMinorUnits,
        string currencyCode,
        string accountType = LedgerAccountTypeNames.Wallet) =>
        BillingEntryFactory.Create(
            organizationId,
            branchId,
            playerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.TopUp,
            accountType,
            amountMinorUnits,
            quantitySeconds: 0,
            currencyCode,
            "seed",
            "test seed",
            reversesLedgerEntryId: null,
            StaffUserId,
            Now);
}
