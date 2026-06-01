using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class EfScheduledReportRunnerTests
{
    // Runs early on 2026-06-02; a daily schedule covers the prior UTC day 2026-06-01.
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

    private static ReportScheduleEntity DailySalesSchedule(DateTimeOffset nextRunUtc, bool isActive = true) => new()
    {
        ReportScheduleId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
        ReportType = ScheduledReportTypeNames.Sales, Frequency = ReportScheduleFrequencyNames.Daily,
        IsActive = isActive, NextRunUtc = nextRunUtc, CreatedByStaffUserId = Guid.NewGuid(),
        CreatedAtUtc = Now.AddDays(-1), UpdatedAtUtc = Now.AddDays(-1),
    };

    private static EfScheduledReportRunner NewRunner(PlatformDbContext db, RecordingNotificationService recorder) =>
        new(db, new EfReportService(db), new EfOrganizationOwnerResolver(db), recorder);

    [Fact]
    public async Task RunAsync_DueSalesSchedule_EnqueuesDigestWithCsvAttachmentAndAdvances()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        var schedule = DailySalesSchedule(Now.AddHours(-6));
        db.ReportSchedules.Add(schedule);
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ShiftId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(), State = PosSaleStateNames.Paid, CurrencyCode = "TJS",
            TotalMinorUnits = 5000, CreatedAtUtc = Yesterday, PaidAtUtc = Yesterday
        });
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, count);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.ScheduledReport, request.TemplateKey);
        Assert.Equal(NotificationCategory.Digest, request.Category);
        Assert.Equal("owner@club.example", request.Recipient.EmailAddress);
        Assert.Equal($"report-scheduled:{schedule.ReportScheduleId:N}:2026-06-01", request.IdempotencyKey);

        var attachment = Assert.Single(request.Attachments!);
        Assert.Equal("sales_2026-06-01.csv", attachment.FileName);
        Assert.Equal("text/csv", attachment.ContentType);
        var csv = Encoding.UTF8.GetString(attachment.Content);
        Assert.Contains("pos_sale_id", csv, StringComparison.Ordinal);
        Assert.Contains("5000", csv, StringComparison.Ordinal);

        var advanced = await db.ReportSchedules.SingleAsync(s => s.ReportScheduleId == schedule.ReportScheduleId);
        Assert.Equal(DateTimeOffset.Parse("2026-06-03T00:00:00Z"), advanced.NextRunUtc);
        Assert.Equal(Now, advanced.LastRunUtc);
        Assert.True(advanced.IsActive);
    }

    [Fact]
    public async Task RunAsync_NotDue_SkippedAndUnchanged()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        var future = Now.AddDays(1);
        db.ReportSchedules.Add(DailySalesSchedule(future));
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(recorder.Requests);
        var unchanged = await db.ReportSchedules.SingleAsync();
        Assert.Equal(future, unchanged.NextRunUtc);
        Assert.Null(unchanged.LastRunUtc);
    }

    [Fact]
    public async Task RunAsync_InactiveSchedule_Skipped()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        db.ReportSchedules.Add(DailySalesSchedule(Now.AddHours(-6), isActive: false));
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task RunAsync_NoOwnerEmail_AdvancesScheduleButSendsNothing()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db, ownerEmail: null);
        var schedule = DailySalesSchedule(Now.AddHours(-6));
        db.ReportSchedules.Add(schedule);
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();

        var count = await NewRunner(db, recorder).RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(recorder.Requests);
        var advanced = await db.ReportSchedules.SingleAsync(s => s.ReportScheduleId == schedule.ReportScheduleId);
        Assert.Equal(DateTimeOffset.Parse("2026-06-03T00:00:00Z"), advanced.NextRunUtc);
        Assert.Equal(Now, advanced.LastRunUtc);
    }
}
