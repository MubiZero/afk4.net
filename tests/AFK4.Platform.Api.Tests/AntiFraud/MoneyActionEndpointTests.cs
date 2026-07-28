using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class MoneyActionEndpointTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid Supervisor = Guid.Parse("d1d1d1d1-d1d1-4d1d-8d1d-d1d1d1d1d1d1");
    private static readonly Guid Manager = Guid.Parse("e2e2e2e2-e2e2-4e2e-8e2e-e2e2e2e2e2e2");
    private static readonly Guid Owner = Guid.Parse("f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3");
    private static readonly Guid Cashier = Guid.Parse("c4c4c4c4-c4c4-4c4c-8c4c-c4c4c4c4c4c4");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T10:00:00Z");

    [Fact]
    public async Task Submit_OverThreshold_HeldForApproval_NoLedgerWrite()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions",
            SubmitRefund(entryId, 6000, "money-1"));
        var body = await response.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("pending_approval", body!.Outcome);
        Assert.NotNull(body.MoneyActionRequestId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var request = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Pending, request.State);
        Assert.Equal(Supervisor, request.RequestedByStaffUserId);
        Assert.Empty(await db.LedgerEntries.Where(e => e.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
    }

    [Fact]
    public async Task Submit_UnderThreshold_ExecutesImmediately()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions",
            SubmitRefund(entryId, 3000, "money-1"));
        var body = await response.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("executed", body!.Outcome);
        Assert.NotNull(body.ResultingLedgerEntryId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.MoneyActionRequests.ToListAsync());
        var refund = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Refund);
        Assert.Equal(-3000, refund.AmountMinorUnits);
        // §5.5: the executed audit carries the amount for actor+amount review.
        var executed = await db.AuditRecords.SingleAsync(a => a.Action == "billing.money_action.executed");
        Assert.Equal(3000, executed.AmountMinorUnits);
        Assert.Equal(Supervisor, executed.ActorStaffUserId);
    }

    [Fact]
    public async Task Approve_ByDifferentManager_ExecutesRefundThroughLedger()
    {
        await using var factory = new PlatformApiFactory();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);

        Guid requestId;
        using (var supervisorClient = factory.CreateClient())
        {
            await AuthorizeAsAsync(factory, supervisorClient, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);
            var submit = await supervisorClient.PostAsJsonAsync(
                $"/api/branches/{TestIds.BranchId:D}/money-actions",
                SubmitRefund(entryId, 6000, "money-1"));
            var submitBody = await submit.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>();
            requestId = submitBody!.MoneyActionRequestId!.Value;
        }

        using var managerClient = factory.CreateClient();
        await AuthorizeAsAsync(factory, managerClient, Manager, "manager@afk4.test", OrganizationRoleNames.BranchManager);
        var approve = await managerClient.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions/{requestId:D}/approve",
            new MoneyActionDecisionRequest("verified receipt"));
        var approveBody = await approve.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>();

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal("approved", approveBody!.Outcome);
        Assert.NotNull(approveBody.ResultingLedgerEntryId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var refund = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Refund);
        Assert.Equal(-6000, refund.AmountMinorUnits);
        Assert.Equal(Supervisor, refund.CreatedByStaffUserId); // attributed to the requester
        var request = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Approved, request.State);
        Assert.Equal(Manager, request.ApprovedByStaffUserId);
        Assert.Equal(refund.LedgerEntryId, request.ResultingLedgerEntryId);
    }

    [Fact]
    public async Task Approve_BySelf_Forbidden_NoLedgerWrite()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);
        // Owner holds both the refund permission and ApproveMoneyAction, but still cannot self-approve.
        await AuthorizeAsAsync(factory, client, Owner, "owner@afk4.test", OrganizationRoleNames.OrganizationOwner);

        var submit = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions",
            SubmitRefund(entryId, 6000, "money-1"));
        var submitBody = await submit.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>();
        var requestId = submitBody!.MoneyActionRequestId!.Value;

        var approve = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions/{requestId:D}/approve",
            new MoneyActionDecisionRequest(null));

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.LedgerEntries.Where(e => e.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
        var request = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Pending, request.State);
    }

    [Fact]
    public async Task Submit_BeyondDailyCap_RejectedUnprocessable()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 30000);
        // Supervisor already spent 18000 of their 20000 daily cap this shift.
        await SeedPriorHighRiskSpendAsync(factory, Supervisor, 18000);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions",
            SubmitRefund(entryId, 3000, "money-1")); // 18000 + 3000 > 20000

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // No NEW refund written (only the seeded 18000 prior-spend entry remains) and nothing held.
        Assert.False(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.Refund && e.AmountMinorUnits == -3000));
        Assert.Empty(await db.MoneyActionRequests.ToListAsync());
    }

    [Fact]
    public async Task ListPending_ReturnsSubmittedRequest_ForManager()
    {
        await using var factory = new PlatformApiFactory();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);

        using (var supervisorClient = factory.CreateClient())
        {
            await AuthorizeAsAsync(factory, supervisorClient, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);
            await supervisorClient.PostAsJsonAsync(
                $"/api/branches/{TestIds.BranchId:D}/money-actions",
                SubmitRefund(entryId, 6000, "money-1"));
        }

        using var managerClient = factory.CreateClient();
        await AuthorizeAsAsync(factory, managerClient, Manager, "manager@afk4.test", OrganizationRoleNames.BranchManager);
        var list = await managerClient.GetFromJsonAsync<MoneyActionRequestListResponse>(
            $"/api/branches/{TestIds.BranchId:D}/money-actions?state=pending");

        Assert.Single(list!.Requests);
        Assert.Equal(6000, list.Requests[0].AmountMinorUnits);
        Assert.Equal(MoneyActionRequestStateNames.Pending, list.Requests[0].State);
    }

    [Fact]
    public async Task Approve_WithoutApprovePermission_Forbidden()
    {
        await using var factory = new PlatformApiFactory();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);

        Guid requestId;
        using (var ownerClient = factory.CreateClient())
        {
            await AuthorizeAsAsync(factory, ownerClient, Owner, "owner@afk4.test", OrganizationRoleNames.OrganizationOwner);
            var submit = await ownerClient.PostAsJsonAsync(
                $"/api/branches/{TestIds.BranchId:D}/money-actions",
                SubmitRefund(entryId, 6000, "money-1"));
            requestId = (await submit.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>())!.MoneyActionRequestId!.Value;
        }

        // Cashier lacks ApproveMoneyAction.
        using var cashierClient = factory.CreateClient();
        await AuthorizeAsAsync(factory, cashierClient, Cashier, "cashier@afk4.test", OrganizationRoleNames.Operator);
        var approve = await cashierClient.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions/{requestId:D}/approve",
            new MoneyActionDecisionRequest(null));

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task Submit_ForInactivePlayer_UnderThreshold_NotExecuted_NoLedgerWrite()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);
        await SetPlayerActiveAsync(factory, isActive: false);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        // Under-threshold would normally execute immediately — the inactive guard must stop it.
        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions",
            SubmitRefund(entryId, 3000, "money-1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.LedgerEntries.Where(e => e.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
        Assert.Empty(await db.MoneyActionRequests.ToListAsync());
    }

    [Fact]
    public async Task Approve_AfterPlayerDeactivated_Blocked_NoLedgerWrite()
    {
        // The hole: an over-threshold refund is held pending while the player is active, then the
        // player is deactivated, then a manager approves. The approval replays the payload through the
        // executor — which must re-check IsActive and refuse, leaving no ledger movement.
        await using var factory = new PlatformApiFactory();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 10000);

        Guid requestId;
        using (var supervisorClient = factory.CreateClient())
        {
            await AuthorizeAsAsync(factory, supervisorClient, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);
            var submit = await supervisorClient.PostAsJsonAsync(
                $"/api/branches/{TestIds.BranchId:D}/money-actions",
                SubmitRefund(entryId, 6000, "money-1"));
            requestId = (await submit.Content.ReadFromJsonAsync<MoneyActionSubmitResponse>())!.MoneyActionRequestId!.Value;
        }

        await SetPlayerActiveAsync(factory, isActive: false);

        using var managerClient = factory.CreateClient();
        await AuthorizeAsAsync(factory, managerClient, Manager, "manager@afk4.test", OrganizationRoleNames.BranchManager);
        var approve = await managerClient.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/money-actions/{requestId:D}/approve",
            new MoneyActionDecisionRequest("verified receipt"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, approve.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.LedgerEntries.Where(e => e.EntryType == LedgerEntryTypeNames.Refund).ToListAsync());
        // The pending request is preserved (not consumed) so it can expire or be retried.
        var request = await db.MoneyActionRequests.SingleAsync();
        Assert.Equal(MoneyActionRequestStateNames.Pending, request.State);
    }

    private static async Task SetPlayerActiveAsync(PlatformApiFactory factory, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var player = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == PlayerAccountId);
        player.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private static MoneyActionSubmitRequest SubmitRefund(Guid ledgerEntryId, long amount, string key) =>
        new(
            OrganizationId: TestIds.OrganizationId,
            ActionType: MoneyActionTypeNames.Refund,
            PlayerAccountId: PlayerAccountId,
            LedgerEntryId: ledgerEntryId,
            AccountType: LedgerAccountTypeNames.Wallet,
            SignedAmountMinorUnits: -amount,
            CurrencyCode: "TJS",
            QuantitySeconds: 0,
            Reason: "duplicate charge refund",
            IdempotencyKey: key);

    private static async Task SeedBaseAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Org",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = Supervisor,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            OpeningNote = "test",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedRefundableTopUpAsync(PlatformApiFactory factory, long amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entry = new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.TopUp,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            Description = "top up",
            Reason = "seed",
            CreatedByStaffUserId = Supervisor,
            CreatedAtUtc = Now
        };
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.LedgerEntryId;
    }

    private static async Task SeedPriorHighRiskSpendAsync(PlatformApiFactory factory, Guid actor, long amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var shiftId = await db.Shifts.Where(s => s.BranchId == TestIds.BranchId).Select(s => s.ShiftId).SingleAsync();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = shiftId,
            PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.Refund,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = -amount,
            CurrencyCode = "TJS",
            Description = "prior refund",
            Reason = "seed",
            CreatedByStaffUserId = actor,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task AuthorizeAsAsync(
        PlatformApiFactory factory, HttpClient client, Guid staffUserId, string email, string role)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var hasher = new PasswordHasher<StaffUserEntity>();
            var user = new StaffUserEntity
            {
                StaffUserId = staffUserId,
                OrganizationId = TestIds.OrganizationId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                DisplayName = email,
                IsActive = true,
                CreatedAtUtc = Now
            };
            user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");
            db.StaffUsers.Add(user);
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = staffUserId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                RoleName = role
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, email, "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }
}
