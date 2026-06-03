using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class EfDailySummaryRunnerTests
{
    // Runner is invoked early on 2026-06-02; the summarized day is the prior UTC day, 2026-06-01.
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T06:00:00Z");
    private static readonly DateTimeOffset Yesterday = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
    private static readonly Guid OrgId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid BranchId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task SeedOrgWithOwnerAsync(PlatformDbContext db, string? ownerEmail = "owner@club.example")
    {
        var ownerId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrgId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId, OrganizationId = OrgId, Name = "Central Branch", CreatedAtUtc = Now
        });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = OrgId, UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = ownerEmail, PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = OrgId, BranchId = BranchId, RoleName = StaffRoleNames.Owner
        });
        await db.SaveChangesAsync();
    }

    private static PosSaleEntity PaidSale(long totalMinorUnits, DateTimeOffset paidAtUtc) => new()
    {
        PosSaleId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ShiftId = Guid.NewGuid(),
        CreatedByStaffUserId = Guid.NewGuid(), State = PosSaleStateNames.Paid, CurrencyCode = "TJS",
        TotalMinorUnits = totalMinorUnits, CreatedAtUtc = paidAtUtc, PaidAtUtc = paidAtUtc
    };

    private static ShiftEntity ClosedShift(long differenceMinorUnits, DateTimeOffset closedAtUtc) => new()
    {
        ShiftId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, State = ShiftStateNames.Closed,
        CurrencyCode = "TJS", StartingCashMinorUnits = 100000, CountedCashMinorUnits = 100000 + differenceMinorUnits,
        ExpectedCashMinorUnits = 100000, DifferenceMinorUnits = differenceMinorUnits,
        OpenedAtUtc = closedAtUtc.AddHours(-8), ClosedAtUtc = closedAtUtc
    };

    private static LedgerEntryEntity RefundEntry(long amountMinorUnits, DateTimeOffset createdAtUtc) => new()
    {
        LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = Guid.NewGuid(),
        EntryType = LedgerEntryTypeNames.Refund, AccountType = LedgerAccountTypeNames.Wallet,
        AmountMinorUnits = amountMinorUnits, CurrencyCode = "TJS",
        CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = createdAtUtc
    };

    private static LedgerEntryEntity WriteOffEntry(long amountMinorUnits, DateTimeOffset createdAtUtc) => new()
    {
        LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PlayerAccountId = Guid.NewGuid(),
        EntryType = LedgerEntryTypeNames.ManualCorrection, AccountType = LedgerAccountTypeNames.Debt,
        AmountMinorUnits = amountMinorUnits, CurrencyCode = "TJS",
        CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = createdAtUtc
    };

    private static AuditRecordEntity CompAudit(DateTimeOffset createdAtUtc, long amount = 0) => new()
    {
        AuditRecordId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
        ActorStaffUserId = Guid.NewGuid(), Action = AuditActionNames.SessionComp, TargetType = "Session",
        Outcome = AuditOutcome.Succeeded, SourceApp = "test", AmountMinorUnits = amount, CreatedAtUtc = createdAtUtc
    };

    private static EfDailySummaryRunner NewRunner(PlatformDbContext db, RecordingNotificationService recorder) =>
        new(db, new EfOrganizationOwnerResolver(db), recorder);

    [Fact]
    public async Task RunAsync_OrgWithActivityYesterday_EnqueuesDigestSummaryToOwner()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        db.PosSales.AddRange(PaidSale(5000, Yesterday), PaidSale(2500, Yesterday));
        db.Shifts.Add(ClosedShift(-2500, Yesterday)); // 25.00 short
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, count);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.OwnerDailySummary, request.TemplateKey);
        Assert.Equal(NotificationCategory.Digest, request.Category);
        Assert.Equal("owner@club.example", request.Recipient.EmailAddress);
        Assert.Equal($"daily-summary:{OrgId:N}:2026-06-01", request.IdempotencyKey);
        Assert.Equal("Demo Club", request.Tokens["organizationName"]);
        Assert.Equal("2026-06-01", request.Tokens["date"]);
        Assert.Equal("75.00", request.Tokens["revenue"]);
        Assert.Equal("2", request.Tokens["salesCount"]);
        Assert.Equal("TJS", request.Tokens["currency"]);
        Assert.Equal("1", request.Tokens["shiftsClosed"]);
        Assert.Equal("1", request.Tokens["discrepancyCount"]);
        Assert.Equal("-25.00", request.Tokens["discrepancyTotal"]);
    }

    [Fact]
    public async Task RunAsync_OnlyAggregatesPriorDayWindow()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        db.PosSales.AddRange(
            PaidSale(5000, Yesterday),                                            // in window
            PaidSale(9999, DateTimeOffset.Parse("2026-06-02T01:00:00Z")),         // today — excluded
            PaidSale(8888, DateTimeOffset.Parse("2026-05-31T23:00:00Z")));        // day before — excluded
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal("50.00", request.Tokens["revenue"]);
        Assert.Equal("1", request.Tokens["salesCount"]);
    }

    [Fact]
    public async Task RunAsync_IncludesAntiFraudRollupTokens()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        db.PosSales.Add(PaidSale(5000, Yesterday));
        db.LedgerEntries.Add(RefundEntry(-4200, Yesterday));
        db.LedgerEntries.Add(WriteOffEntry(-5000, Yesterday));
        db.AuditRecords.Add(CompAudit(Yesterday, 3000));
        db.AuditRecords.Add(CompAudit(Yesterday, 1500));
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal("42.00", request.Tokens["refundTotal"]);
        Assert.Equal("2", request.Tokens["compCount"]);
        Assert.Equal("45.00", request.Tokens["compValueTotal"]);
        Assert.Equal("50.00", request.Tokens["writeOffTotal"]);
        Assert.Equal("0.00", request.Tokens["manualCorrectionTotal"]);
    }

    [Fact]
    public async Task RunAsync_OrgWithOnlyRefundsNoSalesOrShifts_StillSummarizes()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        db.LedgerEntries.Add(RefundEntry(-4200, Yesterday));
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, count);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal("42.00", request.Tokens["refundTotal"]);
    }

    [Fact]
    public async Task RunAsync_NoActivity_SendsNothing()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task RunAsync_ActivityButNoOwnerEmail_SendsNothing()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db, ownerEmail: null);
        db.PosSales.Add(PaidSale(5000, Yesterday));
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(recorder.Requests);
    }
}
