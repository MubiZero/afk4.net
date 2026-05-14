using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfAuditSearchServiceTests
{
    private static readonly Guid OtherBranchId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherOrganizationId = Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc");

    [Fact]
    public async Task SearchAsync_ReturnsBranchScopedRecordsNewestFirstWithFiltersAndLimit()
    {
        await using var db = CreateDbContext();
        var service = new EfAuditSearchService(db);
        var older = SeedRecord(
            db,
            AuditActionNames.StartSession,
            AuditOutcome.Succeeded,
            "Session",
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"));
        var newer = SeedRecord(
            db,
            AuditActionNames.StartSession,
            AuditOutcome.Succeeded,
            "Session",
            DateTimeOffset.Parse("2026-05-14T10:00:00Z"));
        SeedRecord(
            db,
            AuditActionNames.EndSession,
            AuditOutcome.Succeeded,
            "Session",
            DateTimeOffset.Parse("2026-05-14T11:00:00Z"));
        SeedRecord(
            db,
            AuditActionNames.StartSession,
            AuditOutcome.Denied,
            "Session",
            DateTimeOffset.Parse("2026-05-14T12:00:00Z"));
        SeedRecord(
            db,
            AuditActionNames.StartSession,
            AuditOutcome.Succeeded,
            "Session",
            DateTimeOffset.Parse("2026-05-14T13:00:00Z"),
            branchId: OtherBranchId);
        SeedRecord(
            db,
            AuditActionNames.StartSession,
            AuditOutcome.Succeeded,
            "Session",
            DateTimeOffset.Parse("2026-05-14T14:00:00Z"),
            organizationId: OtherOrganizationId);
        await db.SaveChangesAsync();

        var result = await service.SearchAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new AuditSearchQuery(
                Action: AuditActionNames.StartSession,
                Outcome: AuditOutcome.Succeeded,
                TargetType: "Session",
                FromUtc: DateTimeOffset.Parse("2026-05-14T08:30:00Z"),
                ToUtc: DateTimeOffset.Parse("2026-05-14T10:30:00Z"),
                Limit: 1),
            CancellationToken.None);

        var record = Assert.Single(result.Records);
        Assert.Equal(newer.AuditRecordId, record.AuditRecordId);
        Assert.Equal(1, result.Limit);
        Assert.DoesNotContain(result.Records, candidate => candidate.AuditRecordId == older.AuditRecordId);
    }

    [Fact]
    public async Task SearchAsync_ClampsLimitAndIgnoresBlankFilters()
    {
        await using var db = CreateDbContext();
        var service = new EfAuditSearchService(db);
        SeedRecord(
            db,
            AuditActionNames.CreateDeviceEnrollmentCode,
            AuditOutcome.Succeeded,
            "DeviceEnrollmentCode",
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"));
        await db.SaveChangesAsync();

        var result = await service.SearchAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new AuditSearchQuery(
                Action: " ",
                Outcome: "",
                TargetType: null,
                FromUtc: null,
                ToUtc: null,
                Limit: 500),
            CancellationToken.None);

        Assert.Equal(200, result.Limit);
        Assert.Single(result.Records);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static AuditRecordEntity SeedRecord(
        PlatformDbContext db,
        string action,
        string outcome,
        string targetType,
        DateTimeOffset createdAtUtc,
        Guid? branchId = null,
        Guid? organizationId = null)
    {
        var record = new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = organizationId ?? TestIds.OrganizationId,
            BranchId = branchId ?? TestIds.BranchId,
            ActorStaffUserId = TestIds.TechnicianStaffUserId,
            Action = action,
            TargetType = targetType,
            TargetId = Guid.NewGuid().ToString("D"),
            Outcome = outcome,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = createdAtUtc
        };

        db.AuditRecords.Add(record);
        return record;
    }
}
