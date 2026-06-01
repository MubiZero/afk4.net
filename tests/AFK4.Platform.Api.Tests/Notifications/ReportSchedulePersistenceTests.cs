using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class ReportSchedulePersistenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task ReportSchedule_RoundTripsAndIsQueryableByDueWindow()
    {
        await using var db = NewContext();
        var dueId = Guid.NewGuid();
        db.ReportSchedules.AddRange(
            new ReportScheduleEntity
            {
                ReportScheduleId = dueId, OrganizationId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
                ReportType = ScheduledReportTypeNames.Sales, Frequency = ReportScheduleFrequencyNames.Daily,
                IsActive = true, NextRunUtc = Now.AddHours(-1), CreatedByStaffUserId = Guid.NewGuid(),
                CreatedAtUtc = Now, UpdatedAtUtc = Now,
            },
            new ReportScheduleEntity
            {
                ReportScheduleId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
                ReportType = ScheduledReportTypeNames.Shifts, Frequency = ReportScheduleFrequencyNames.Weekly,
                IsActive = false, NextRunUtc = Now.AddHours(-1), CreatedByStaffUserId = Guid.NewGuid(),
                CreatedAtUtc = Now, UpdatedAtUtc = Now,
            });
        await db.SaveChangesAsync();

        var due = await db.ReportSchedules
            .Where(schedule => schedule.IsActive && schedule.NextRunUtc <= Now)
            .ToListAsync();

        var loaded = Assert.Single(due);
        Assert.Equal(dueId, loaded.ReportScheduleId);
        Assert.Equal(ScheduledReportTypeNames.Sales, loaded.ReportType);
        Assert.Equal(ReportScheduleFrequencyNames.Daily, loaded.Frequency);
    }
}
