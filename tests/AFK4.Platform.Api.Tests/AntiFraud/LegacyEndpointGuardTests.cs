using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.AntiFraud;

/// <summary>
/// Anti-fraud §5.2 enforcement: the legacy direct ledger endpoints (<c>/ledger/{id}/refunds</c> and
/// <c>/ledger/manual-corrections</c>) must route through the same <see cref="MoneyActionGuard"/> as the
/// <c>/money-actions</c> front door. A lone supervisor can no longer push an over-threshold or
/// over-cap action straight to the ledger by calling the old endpoint — it is held (must use the
/// approval front door) or rejected, with no ledger write. Under-threshold actions still execute.
/// </summary>
public sealed class LegacyEndpointGuardTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid Supervisor = Guid.Parse("d1d1d1d1-d1d1-4d1d-8d1d-d1d1d1d1d1d1");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T10:00:00Z");

    [Fact]
    public async Task LegacyManualCorrection_OverThreshold_Blocked_NoLedgerWrite()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/manual-corrections",
            new ManualLedgerCorrectionRequest(
                TestIds.OrganizationId,
                LedgerAccountTypeNames.Wallet,
                new MoneyDto("TJS", -10000), // 10000 > 5000 default approval threshold
                0,
                "manager correction for dispute",
                "correction-over"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.LedgerEntries.ToListAsync());
        // A blocked attempt leaves an audit trail.
        var audit = await db.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.MoneyActionRequested, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task LegacyManualCorrection_OverDailyCap_Rejected_NoLedgerWrite()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        // Supervisor already spent 18000 of their 20000 daily cap this shift.
        await SeedPriorHighRiskSpendAsync(factory, Supervisor, 18000);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/manual-corrections",
            new ManualLedgerCorrectionRequest(
                TestIds.OrganizationId,
                LedgerAccountTypeNames.Wallet,
                new MoneyDto("TJS", -3000), // 18000 + 3000 > 20000 daily cap
                0,
                "manager correction for dispute",
                "correction-cap"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Only the seeded prior-spend entry remains; no new manual correction.
        Assert.False(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.ManualCorrection));
    }

    [Fact]
    public async Task LegacyManualCorrection_UnderThreshold_StillExecutes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/manual-corrections",
            new ManualLedgerCorrectionRequest(
                TestIds.OrganizationId,
                LedgerAccountTypeNames.Wallet,
                new MoneyDto("TJS", -300), // under 5000 threshold and 20000 cap
                0,
                "manager correction for dispute",
                "correction-ok"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entry = await db.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryTypeNames.ManualCorrection, entry.EntryType);
        Assert.Equal(-300, entry.AmountMinorUnits);
        // Unchanged happy-path audit (single record, the correction itself).
        var audit = await db.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ManualLedgerCorrection, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task LegacyRefund_OverThreshold_Blocked_NoReversal()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 30000);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/{entryId:D}/refunds",
            new RefundLedgerEntryRequest(
                TestIds.OrganizationId,
                entryId,
                new MoneyDto("TJS", 6000), // 6000 > 5000 default approval threshold
                "duplicate charge refund",
                "refund-over"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.Refund));
    }

    [Fact]
    public async Task LegacyRefund_UnderThreshold_StillExecutes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedBaseAsync(factory);
        var entryId = await SeedRefundableTopUpAsync(factory, 30000);
        await AuthorizeAsAsync(factory, client, Supervisor, "supervisor@afk4.test", OrganizationRoleNames.ShiftSupervisor);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/{entryId:D}/refunds",
            new RefundLedgerEntryRequest(
                TestIds.OrganizationId,
                entryId,
                new MoneyDto("TJS", 3000), // under 5000 threshold
                "duplicate charge refund",
                "refund-ok"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var refund = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Refund);
        Assert.Equal(-3000, refund.AmountMinorUnits);
    }

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
