using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class EfMoneyActionPolicyResolverTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherStaffUserId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid OpenShiftId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    [Fact]
    public async Task Assess_SupervisorBelowThreshold_ExecutesNow()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        var resolver = CreateResolver(db);

        var assessment = await Assess(resolver, [StaffRoleNames.ShiftSupervisor], amount: -4000);

        Assert.Equal(MoneyActionDecision.ExecuteNow, assessment.Decision);
        Assert.Equal(4000, assessment.AmountMinorUnits);
    }

    [Fact]
    public async Task Assess_SupervisorAboveThreshold_RequiresApproval()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        var resolver = CreateResolver(db);

        var assessment = await Assess(resolver, [StaffRoleNames.ShiftSupervisor], amount: -6000);

        Assert.Equal(MoneyActionDecision.RequireApproval, assessment.Decision);
    }

    [Fact]
    public async Task Assess_SupervisorWouldBreachDailyCap_Rejects()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        await SeedHighRiskEntryAsync(db, ActorStaffUserId, LedgerEntryTypeNames.Refund, -18000);
        var resolver = CreateResolver(db);

        // 18000 already spent + 3000 > 20000 daily cap.
        var assessment = await Assess(resolver, [StaffRoleNames.ShiftSupervisor], amount: -3000);

        Assert.Equal(MoneyActionDecision.Reject, assessment.Decision);
        Assert.Equal(18000, assessment.SpentTodayMinorUnits);
    }

    [Fact]
    public async Task Assess_CashierZeroCap_RejectsAnyRefund()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        var resolver = CreateResolver(db);

        var assessment = await Assess(resolver, [StaffRoleNames.CashierOperator], amount: -1000);

        Assert.Equal(MoneyActionDecision.Reject, assessment.Decision);
    }

    [Fact]
    public async Task Assess_OwnerUncapped_OverThreshold_RequiresApprovalNotReject()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        var resolver = CreateResolver(db);

        var assessment = await Assess(resolver, [StaffRoleNames.Owner], amount: -999_999);

        Assert.Equal(MoneyActionDecision.RequireApproval, assessment.Decision);
    }

    [Fact]
    public async Task Assess_BranchThresholdOverride_RaisesExecuteCeiling()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db, approvalThreshold: 10000);
        var resolver = CreateResolver(db);

        var assessment = await Assess(resolver, [StaffRoleNames.ShiftSupervisor], amount: -6000);

        Assert.Equal(MoneyActionDecision.ExecuteNow, assessment.Decision);
    }

    [Fact]
    public async Task Assess_DebtReducingCorrection_ClassifiedAsWriteOff()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        var resolver = CreateResolver(db);

        var assessment = await resolver.AssessAsync(
            TestIds.OrganizationId, TestIds.BranchId, ActorStaffUserId, [StaffRoleNames.ShiftSupervisor],
            MoneyActionType.ManualCorrection, LedgerAccountTypeNames.Debt, signedAmountMinorUnits: -4000,
            CancellationToken.None);

        Assert.Equal(MoneyActionType.DebtWriteOff, assessment.EffectiveActionType);
    }

    [Fact]
    public async Task Assess_SpentToday_OnlyCountsThisActorsHighRiskEntriesInOpenShift()
    {
        await using var db = CreateDbContext();
        await SeedBranchAsync(db);
        await SeedHighRiskEntryAsync(db, ActorStaffUserId, LedgerEntryTypeNames.Refund, -5000);
        await SeedHighRiskEntryAsync(db, ActorStaffUserId, LedgerEntryTypeNames.ManualCorrection, -2000);
        await SeedHighRiskEntryAsync(db, ActorStaffUserId, LedgerEntryTypeNames.GameplayCharge, -9000); // not high-risk
        await SeedHighRiskEntryAsync(db, OtherStaffUserId, LedgerEntryTypeNames.Refund, -7000); // other actor
        var resolver = CreateResolver(db);

        var assessment = await Assess(resolver, [StaffRoleNames.ShiftSupervisor], amount: -1000);

        Assert.Equal(7000, assessment.SpentTodayMinorUnits); // 5000 + 2000 only
    }

    private static Task<MoneyActionAssessment> Assess(
        IMoneyActionPolicyResolver resolver, string[] roles, long amount) =>
        resolver.AssessAsync(
            TestIds.OrganizationId, TestIds.BranchId, ActorStaffUserId, roles,
            MoneyActionType.Refund, LedgerAccountTypeNames.Wallet, amount, CancellationToken.None);

    private static async Task SeedBranchAsync(PlatformDbContext db, long? approvalThreshold = null)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Slug = "main",
            Name = "Main",
            City = "Dushanbe",
            HighRiskApprovalThresholdMinorUnits = approvalThreshold,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedHighRiskEntryAsync(
        PlatformDbContext db, Guid actor, string entryType, long amount)
    {
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = OpenShiftId,
            PlayerAccountId = Guid.NewGuid(),
            EntryType = entryType,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "seed",
            CreatedByStaffUserId = actor,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfMoneyActionPolicyResolver CreateResolver(PlatformDbContext db) =>
        new(db, new FixedOpenShiftResolver(OpenShiftId));

    private sealed class FixedOpenShiftResolver(Guid openShiftId) : IOpenShiftResolver
    {
        public Task<BillingCommandServiceResult<Guid>> GetOpenShiftIdAsync(
            Guid organizationId, Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(BillingCommandServiceResult<Guid>.Ok(openShiftId));
    }
}
