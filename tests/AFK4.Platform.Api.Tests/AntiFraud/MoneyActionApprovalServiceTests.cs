using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class MoneyActionApprovalServiceTests
{
    private static readonly Guid Requester = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Approver = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ShiftId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid PlayerId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid LedgerEntryId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T12:00:00Z");

    private static MoneyActionCommand RefundCommand(long amount = 6000, string key = "refund-1") =>
        new(
            RequestedActionType: MoneyActionType.Refund,
            PlayerAccountId: PlayerId,
            LedgerEntryId: LedgerEntryId,
            AccountType: LedgerAccountTypeNames.Wallet,
            SignedAmountMinorUnits: -amount,
            CurrencyCode: "TJS",
            QuantitySeconds: 0,
            Reason: "duplicate charge",
            IdempotencyKey: key);

    // --- RequestAsync routing ---

    [Fact]
    public async Task Request_ExecuteNow_ExecutesAndCreatesNoPendingRow()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.ExecuteNow), executor);

        var result = await service.RequestAsync(
            TestIds.OrganizationId, TestIds.BranchId, ShiftId, Requester, [OrganizationRoleNames.ShiftSupervisor],
            RefundCommand(), CancellationToken.None);

        Assert.Equal(MoneyActionRequestOutcome.Executed, result.Outcome);
        Assert.Equal(LedgerEntryId, result.ResultingLedgerEntryId);
        Assert.Equal(1, executor.CallCount);
        Assert.Empty(await db.MoneyActionRequests.ToListAsync());
    }

    [Fact]
    public async Task Request_RequireApproval_CreatesPendingRowAndDoesNotExecute()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), executor);

        var result = await service.RequestAsync(
            TestIds.OrganizationId, TestIds.BranchId, ShiftId, Requester, [OrganizationRoleNames.ShiftSupervisor],
            RefundCommand(amount: 6000), CancellationToken.None);

        Assert.Equal(MoneyActionRequestOutcome.PendingApproval, result.Outcome);
        Assert.NotNull(result.MoneyActionRequestId);
        Assert.Equal(0, executor.CallCount);

        var row = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Pending, row.State);
        Assert.Equal(Requester, row.RequestedByStaffUserId);
        Assert.Equal(6000, row.AmountMinorUnits);
        Assert.Equal("duplicate charge", row.Reason);
        Assert.Equal(Now.AddHours(24), row.ExpiresAtUtc);
        Assert.Null(row.ApprovedByStaffUserId);
    }

    [Fact]
    public async Task Request_Reject_DoesNotExecuteOrPersist()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.Reject), executor);

        var result = await service.RequestAsync(
            TestIds.OrganizationId, TestIds.BranchId, ShiftId, Requester, [OrganizationRoleNames.Operator],
            RefundCommand(), CancellationToken.None);

        Assert.Equal(MoneyActionRequestOutcome.Rejected, result.Outcome);
        Assert.Equal(0, executor.CallCount);
        Assert.Empty(await db.MoneyActionRequests.ToListAsync());
    }

    // --- ApproveAsync ---

    [Fact]
    public async Task Approve_ByDifferentUser_ExecutesAndMarksApproved()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), executor);
        var requestId = await SeedPendingAsync(service);

        var result = await service.ApproveAsync(
            TestIds.OrganizationId, TestIds.BranchId, requestId, Approver, "looks legit", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(LedgerEntryId, result.ResultingLedgerEntryId);
        Assert.Equal(1, executor.CallCount);

        var row = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Approved, row.State);
        Assert.Equal(Approver, row.ApprovedByStaffUserId);
        Assert.Equal(LedgerEntryId, row.ResultingLedgerEntryId);
        Assert.Equal(Now, row.DecidedAtUtc);
        Assert.Equal("looks legit", row.DecisionReason);
    }

    [Fact]
    public async Task Approve_BySelf_IsForbidden_NoExecution()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), executor);
        var requestId = await SeedPendingAsync(service);

        var result = await service.ApproveAsync(
            TestIds.OrganizationId, TestIds.BranchId, requestId, Requester, null, CancellationToken.None);

        Assert.True(result.Forbidden);
        Assert.Equal(0, executor.CallCount);
        var row = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Pending, row.State);
    }

    [Fact]
    public async Task Approve_Expired_MarksExpired_NoExecution()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var clock = new MutableClock(Now);
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), executor, clock);
        var requestId = await SeedPendingAsync(service);

        clock.Now = Now.AddHours(25); // past the 24h expiry

        var result = await service.ApproveAsync(
            TestIds.OrganizationId, TestIds.BranchId, requestId, Approver, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, executor.CallCount);
        var row = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Expired, row.State);
    }

    [Fact]
    public async Task Approve_Twice_IsIdempotent_ReturnsSameLedgerEntry()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), executor);
        var requestId = await SeedPendingAsync(service);

        var first = await service.ApproveAsync(
            TestIds.OrganizationId, TestIds.BranchId, requestId, Approver, null, CancellationToken.None);
        var second = await service.ApproveAsync(
            TestIds.OrganizationId, TestIds.BranchId, requestId, Approver, null, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.ResultingLedgerEntryId, second.ResultingLedgerEntryId);
        Assert.Equal(1, executor.CallCount); // executed exactly once
    }

    [Fact]
    public async Task Approve_UnknownRequest_NotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), new FakeExecutor());

        var result = await service.ApproveAsync(
            TestIds.OrganizationId, TestIds.BranchId, Guid.NewGuid(), Approver, null, CancellationToken.None);

        Assert.True(result.NotFound);
    }

    // --- RejectAsync ---

    [Fact]
    public async Task Reject_MarksRejected_NoExecution()
    {
        await using var db = CreateDbContext();
        var executor = new FakeExecutor();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), executor);
        var requestId = await SeedPendingAsync(service);

        var result = await service.RejectAsync(
            TestIds.OrganizationId, TestIds.BranchId, requestId, Approver, "no receipt", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, executor.CallCount);
        var row = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Rejected, row.State);
        Assert.Equal(Approver, row.ApprovedByStaffUserId);
        Assert.Equal("no receipt", row.DecisionReason);
    }

    // --- ListPending ---

    [Fact]
    public async Task ListPending_ReturnsOnlyPending()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, new FakeResolver(MoneyActionDecision.RequireApproval), new FakeExecutor());
        var pending = await SeedPendingAsync(service, "refund-a");
        var toReject = await SeedPendingAsync(service, "refund-b");
        await service.RejectAsync(TestIds.OrganizationId, TestIds.BranchId, toReject, Approver, "no", CancellationToken.None);

        var list = await service.ListPendingAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        Assert.Single(list);
        Assert.Equal(pending, list[0].MoneyActionRequestId);
    }

    private async Task<Guid> SeedPendingAsync(IMoneyActionApprovalService service, string key = "refund-1")
    {
        var result = await service.RequestAsync(
            TestIds.OrganizationId, TestIds.BranchId, ShiftId, Requester, [OrganizationRoleNames.ShiftSupervisor],
            RefundCommand(key: key), CancellationToken.None);
        Assert.Equal(MoneyActionRequestOutcome.PendingApproval, result.Outcome);
        return result.MoneyActionRequestId!.Value;
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static MoneyActionApprovalService CreateService(
        PlatformDbContext db, IMoneyActionPolicyResolver resolver, IMoneyActionExecutor executor, TimeProvider? clock = null) =>
        new(db, resolver, executor, clock ?? new MutableClock(Now));

    private sealed class FakeResolver(MoneyActionDecision decision) : IMoneyActionPolicyResolver
    {
        public Task<MoneyActionAssessment> AssessAsync(
            Guid organizationId, Guid branchId, Guid actorStaffUserId, IReadOnlyCollection<string> actorRoleNames,
            MoneyActionType requestedActionType, string accountType, long signedAmountMinorUnits, CancellationToken cancellationToken)
        {
            var effective = MoneyActionGuard.Classify(requestedActionType, accountType, signedAmountMinorUnits);
            var policy = new MoneyActionPolicy(5000, null, null);
            return Task.FromResult(new MoneyActionAssessment(effective, decision, policy, Math.Abs(signedAmountMinorUnits), 0));
        }
    }

    private sealed class FakeExecutor : IMoneyActionExecutor
    {
        public int CallCount { get; private set; }

        public Task<MoneyActionExecutionResult> ExecuteAsync(
            Guid organizationId, Guid branchId, Guid actorStaffUserId, MoneyActionType effectiveType,
            MoneyActionCommand command, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(MoneyActionExecutionResult.Ok(LedgerEntryId));
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
