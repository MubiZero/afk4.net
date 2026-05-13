using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfBillingCommandServiceTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherPlayerAccountId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task CreatePlayerAccountAsync_CreatesActivePlayerAccountAndIsIdempotent()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new CreatePlayerAccountRequest(
            TestIds.OrganizationId,
            "Player One",
            "+992000000001",
            "player-create-001");

        var first = await service.CreatePlayerAccountAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CreatePlayerAccountAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.PlayerAccountId, second.Response.PlayerAccountId);

        var player = await db.PlayerAccounts.SingleAsync();
        Assert.Equal(first.Response.PlayerAccountId, player.PlayerAccountId);
        Assert.True(player.IsActive);
        Assert.Equal(TestIds.BranchId, player.HomeBranchId);
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task CreatePlayerAccountAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var firstRequest = new CreatePlayerAccountRequest(
            TestIds.OrganizationId,
            "Player One",
            "+992000000001",
            "player-create-001");
        var differentRequest = firstRequest with { DisplayName = "Player Two" };

        await service.CreatePlayerAccountAsync(TestIds.BranchId, ActorStaffUserId, firstRequest, CancellationToken.None);
        var conflict = await service.CreatePlayerAccountAsync(TestIds.BranchId, ActorStaffUserId, differentRequest, CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Single(await db.PlayerAccounts.ToListAsync());
    }

    [Fact]
    public async Task TopUpWalletAsync_AppendsTopUpEntryAndReturnsDerivedBalance()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var request = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 5000),
            "front desk cash top-up",
            "topup-001");

        var result = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var entry = await db.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryTypeNames.TopUp, entry.EntryType);
        Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
        Assert.Equal(5000, entry.AmountMinorUnits);
        Assert.Equal(0, entry.QuantitySeconds);
        Assert.Equal("TJS", entry.CurrencyCode);
        Assert.Equal(PlayerAccountId, entry.PlayerAccountId);
        Assert.Equal(ActorStaffUserId, entry.CreatedByStaffUserId);
        Assert.Equal(5000, result.Response.WalletBalance.MinorUnits);
        Assert.Equal(0, result.Response.DebtBalance.MinorUnits);
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task TopUpWalletAsync_RepeatedWithSameRequestReturnsOriginalSummaryWithoutDuplicateEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var request = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 5000),
            "front desk cash top-up",
            "topup-001");

        var first = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Single(await db.LedgerEntries.ToListAsync());
        Assert.Equal(first.Response.WalletBalance, second.Response.WalletBalance);
    }

    [Fact]
    public async Task TopUpWalletAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var firstRequest = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 5000),
            "front desk cash top-up",
            "topup-001");
        var differentRequest = firstRequest with { Amount = new MoneyDto("TJS", 7000) };

        await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, firstRequest, CancellationToken.None);
        var conflict = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, differentRequest, CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Single(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task TopUpWalletAsync_ReusingIdempotencyKeyWithSameRequestAgainstDifferentPlayerReturnsConflict()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerAsync(db, OtherPlayerAccountId, "Player Two");
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var request = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 5000),
            "front desk cash top-up",
            "topup-001");

        await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.TopUpWalletAsync(OtherPlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(conflict.Conflict);
        var entry = await db.LedgerEntries.SingleAsync();
        Assert.Equal(PlayerAccountId, entry.PlayerAccountId);
    }

    [Fact]
    public async Task RefundLedgerEntryAsync_AppendsRefundAndDoesNotMutateOriginal()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var original = CreateLedgerEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0);
        db.LedgerEntries.Add(original);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new RefundLedgerEntryRequest(
            TestIds.OrganizationId,
            original.LedgerEntryId,
            new MoneyDto("TJS", 5000),
            "operator refund approved",
            "refund-001");

        var result = await service.RefundLedgerEntryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        var refund = await db.LedgerEntries.SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.Refund);
        Assert.Equal(LedgerAccountTypeNames.Wallet, refund.AccountType);
        Assert.Equal(-5000, refund.AmountMinorUnits);
        Assert.Equal(original.LedgerEntryId, refund.ReversesLedgerEntryId);
        var originalAfterRefund = await db.LedgerEntries.SingleAsync(entry => entry.LedgerEntryId == original.LedgerEntryId);
        Assert.Equal(5000, originalAfterRefund.AmountMinorUnits);
    }

    [Fact]
    public async Task RefundLedgerEntryAsync_RejectsRefundOverOriginalAmount()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var original = CreateLedgerEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0);
        db.LedgerEntries.Add(original);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new RefundLedgerEntryRequest(
            TestIds.OrganizationId,
            original.LedgerEntryId,
            new MoneyDto("TJS", 6000),
            "operator refund approved",
            "refund-001");

        var result = await service.RefundLedgerEntryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
    }

    [Fact]
    public async Task RefundLedgerEntryAsync_RefundsNegativeGameplayChargeWithPositiveRefundEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var original = CreateLedgerEntry(LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -5000, 0);
        db.LedgerEntries.Add(original);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new RefundLedgerEntryRequest(
            TestIds.OrganizationId,
            original.LedgerEntryId,
            new MoneyDto("TJS", 5000),
            "operator refund approved",
            "refund-001");

        var result = await service.RefundLedgerEntryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        var refund = await db.LedgerEntries.SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.Refund);
        Assert.Equal(5000, refund.AmountMinorUnits);
        Assert.Equal(original.LedgerEntryId, refund.ReversesLedgerEntryId);
    }

    [Fact]
    public async Task RefundLedgerEntryAsync_RejectsCumulativeRefundOverOriginalAmount()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var original = CreateLedgerEntry(LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -5000, 0);
        db.LedgerEntries.Add(original);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var first = await service.RefundLedgerEntryAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new RefundLedgerEntryRequest(
                TestIds.OrganizationId,
                original.LedgerEntryId,
                new MoneyDto("TJS", 3000),
                "operator refund approved",
                "refund-001"),
            CancellationToken.None);
        var second = await service.RefundLedgerEntryAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new RefundLedgerEntryRequest(
                TestIds.OrganizationId,
                original.LedgerEntryId,
                new MoneyDto("TJS", 3000),
                "operator refund approved",
                "refund-002"),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.False(second.Conflict);
        Assert.Single(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task RefundLedgerEntryAsync_NullOrWhitespaceReasonReturnsInvalidAndDoesNotAppendRefund(string? reason)
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var original = CreateLedgerEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0);
        db.LedgerEntries.Add(original);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new RefundLedgerEntryRequest(
            TestIds.OrganizationId,
            original.LedgerEntryId,
            new MoneyDto("TJS", 5000),
            reason!,
            "refund-001");

        var result = await service.RefundLedgerEntryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.False(result.NotFound);
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
    }

    [Fact]
    public async Task TopUpWalletAsync_RejectsMismatchedLedgerCurrencyWithoutAppendingEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        db.LedgerEntries.Add(CreateLedgerEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0, currencyCode: "TJS"));
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("USD", 5000),
            "front desk cash top-up",
            "topup-001");

        var result = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Single(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task TopUpWalletAsync_TreatsExistingLedgerCurrenciesCaseInsensitively()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        db.LedgerEntries.Add(CreateLedgerEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0, currencyCode: "tjs"));
        await SeedOpenShiftAsync(db, saveChanges: false);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 5000),
            "front desk cash top-up",
            "topup-001");

        var result = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task ManualCorrectionAsync_AppendsManualCorrectionAndRequiresReason()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var request = new ManualLedgerCorrectionRequest(
            TestIds.OrganizationId,
            LedgerAccountTypeNames.Wallet,
            new MoneyDto("TJS", -300),
            QuantitySeconds: 0,
            "manager correction for dispute",
            "correction-001");

        var result = await service.ManualCorrectionAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var invalid = await service.ManualCorrectionAsync(
            PlayerAccountId,
            TestIds.BranchId,
            ActorStaffUserId,
            request with { IdempotencyKey = "correction-002", Reason = "short" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var entry = await db.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryTypeNames.ManualCorrection, entry.EntryType);
        Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
        Assert.Equal(-300, result.Response.WalletBalance.MinorUnits);
        Assert.False(invalid.Succeeded);
        Assert.False(invalid.Conflict);
        Assert.Single(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task ManualCorrectionAsync_ReusingIdempotencyKeyWithSameRequestAgainstDifferentPlayerReturnsConflict()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerAsync(db, OtherPlayerAccountId, "Player Two");
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var request = new ManualLedgerCorrectionRequest(
            TestIds.OrganizationId,
            LedgerAccountTypeNames.Wallet,
            new MoneyDto("TJS", -300),
            QuantitySeconds: 0,
            "manager correction for dispute",
            "correction-001");

        await service.ManualCorrectionAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.ManualCorrectionAsync(OtherPlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(conflict.Conflict);
        var entry = await db.LedgerEntries.SingleAsync();
        Assert.Equal(PlayerAccountId, entry.PlayerAccountId);
    }

    [Fact]
    public async Task ManualCorrectionAsync_NullReasonReturnsInvalidAndDoesNotAppendEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        var service = CreateService(db);
        var request = new ManualLedgerCorrectionRequest(
            TestIds.OrganizationId,
            LedgerAccountTypeNames.Wallet,
            new MoneyDto("TJS", -300),
            QuantitySeconds: 0,
            null!,
            "correction-001");

        var result = await service.ManualCorrectionAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.False(result.NotFound);
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task ManualCorrectionAsync_RejectsMismatchedLedgerCurrencyWithoutAppendingEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        db.LedgerEntries.Add(CreateLedgerEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, 0, currencyCode: "TJS"));
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new ManualLedgerCorrectionRequest(
            TestIds.OrganizationId,
            LedgerAccountTypeNames.Wallet,
            new MoneyDto("USD", -300),
            QuantitySeconds: 0,
            "manager correction for dispute",
            "correction-001");

        var result = await service.ManualCorrectionAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Single(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task ManualCorrectionAsync_RejectsPackageTimeWithoutAppendingEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        var service = CreateService(db);
        var request = new ManualLedgerCorrectionRequest(
            TestIds.OrganizationId,
            LedgerAccountTypeNames.PackageTime,
            new MoneyDto("TJS", 0),
            QuantitySeconds: 300,
            "manager correction for dispute",
            "correction-001");

        var result = await service.ManualCorrectionAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task PayDebtAsync_AppendsDebtPaymentAndRejectsOverpayment()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        db.LedgerEntries.Add(CreateLedgerEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 1000, 0));
        await SeedOpenShiftAsync(db, saveChanges: false);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new PayDebtRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 700),
            "cash debt payment",
            "debt-pay-001");

        var result = await service.PayDebtAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var overpayment = await service.PayDebtAsync(
            PlayerAccountId,
            TestIds.BranchId,
            ActorStaffUserId,
            request with { Amount = new MoneyDto("TJS", 400), IdempotencyKey = "debt-pay-002" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var payment = await db.LedgerEntries.SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment);
        Assert.Equal(LedgerAccountTypeNames.Debt, payment.AccountType);
        Assert.Equal(-700, payment.AmountMinorUnits);
        Assert.Equal(300, result.Response.DebtBalance.MinorUnits);
        Assert.False(overpayment.Succeeded);
        Assert.False(overpayment.Conflict);
        Assert.Single(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment).ToListAsync());
    }

    [Fact]
    public async Task PayDebtAsync_ReusingIdempotencyKeyWithSameRequestAgainstDifferentPlayerReturnsConflict()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerAsync(db, OtherPlayerAccountId, "Player Two");
        db.LedgerEntries.Add(CreateLedgerEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 1000, 0));
        db.LedgerEntries.Add(CreateLedgerEntry(
            LedgerEntryTypeNames.PostpaidDebt,
            LedgerAccountTypeNames.Debt,
            1000,
            0,
            OtherPlayerAccountId));
        await SeedOpenShiftAsync(db, saveChanges: false);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new PayDebtRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 700),
            "cash debt payment",
            "debt-pay-001");

        await service.PayDebtAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.PayDebtAsync(OtherPlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(conflict.Conflict);
        var payment = await db.LedgerEntries.SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment);
        Assert.Equal(PlayerAccountId, payment.PlayerAccountId);
    }

    [Fact]
    public async Task PayDebtAsync_RejectsMismatchedLedgerCurrencyWithoutAppendingPayment()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        db.LedgerEntries.Add(CreateLedgerEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 1000, 0, currencyCode: "TJS"));
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new PayDebtRequest(
            TestIds.OrganizationId,
            new MoneyDto("USD", 700),
            "cash debt payment",
            "debt-pay-001");

        var result = await service.PayDebtAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment).ToListAsync());
    }

    [Fact]
    public async Task UnknownPlayerCommands_ReturnNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var unknownPlayerId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

        var topUp = await service.TopUpWalletAsync(
            unknownPlayerId,
            TestIds.BranchId,
            ActorStaffUserId,
            new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "front desk cash top-up", "topup-001"),
            CancellationToken.None);
        var correction = await service.ManualCorrectionAsync(
            unknownPlayerId,
            TestIds.BranchId,
            ActorStaffUserId,
            new ManualLedgerCorrectionRequest(TestIds.OrganizationId, LedgerAccountTypeNames.Wallet, new MoneyDto("TJS", -300), 0, "manager correction for dispute", "correction-001"),
            CancellationToken.None);
        var debtPayment = await service.PayDebtAsync(
            unknownPlayerId,
            TestIds.BranchId,
            ActorStaffUserId,
            new PayDebtRequest(TestIds.OrganizationId, new MoneyDto("TJS", 700), "cash debt payment", "debt-pay-001"),
            CancellationToken.None);

        Assert.True(topUp.NotFound);
        Assert.True(correction.NotFound);
        Assert.True(debtPayment.NotFound);
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfBillingCommandService CreateService(PlatformDbContext db)
    {
        return new EfBillingCommandService(db, new EfShiftService(db, new FixedTimeProvider(Now)), new FixedTimeProvider(Now));
    }

    private static async Task SeedPlayerAsync(
        PlatformDbContext db,
        Guid? playerAccountId = null,
        string displayName = "Player One")
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId ?? PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = displayName,
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedOpenShiftAsync(PlatformDbContext db, bool saveChanges = true)
    {
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });

        if (saveChanges)
        {
            await db.SaveChangesAsync();
        }
    }

    private static LedgerEntryEntity CreateLedgerEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        Guid? playerAccountId = null,
        string currencyCode = "TJS")
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
            QuantitySeconds = quantitySeconds,
            CurrencyCode = currencyCode,
            Description = entryType,
            Reason = "test seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
