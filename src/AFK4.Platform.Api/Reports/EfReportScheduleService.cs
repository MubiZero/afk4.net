using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reports;

public sealed class EfReportScheduleService(PlatformDbContext dbContext, TimeProvider timeProvider) : IReportScheduleService
{
    public async Task<ReportScheduleDto> CreateAsync(
        Guid organizationId,
        Guid branchId,
        Guid createdByStaffUserId,
        string reportType,
        string frequency,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var schedule = new ReportScheduleEntity
        {
            ReportScheduleId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ReportType = reportType,
            Frequency = frequency,
            IsActive = true,
            // Fire on the next tick so the latest complete window is delivered promptly; the runner
            // then advances to the regular frequency boundary.
            NextRunUtc = now,
            CreatedByStaffUserId = createdByStaffUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.ReportSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(schedule);
    }

    public async Task<IReadOnlyList<ReportScheduleDto>> ListAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var schedules = await dbContext.ReportSchedules
            .AsNoTracking()
            .Where(schedule => schedule.OrganizationId == organizationId && schedule.BranchId == branchId)
            .OrderByDescending(schedule => schedule.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return schedules.Select(ToDto).ToList();
    }

    public async Task<bool> DeleteAsync(
        Guid organizationId,
        Guid branchId,
        Guid reportScheduleId,
        CancellationToken cancellationToken)
    {
        var schedule = await dbContext.ReportSchedules
            .FirstOrDefaultAsync(
                candidate => candidate.ReportScheduleId == reportScheduleId
                    && candidate.OrganizationId == organizationId
                    && candidate.BranchId == branchId,
                cancellationToken);
        if (schedule is null)
        {
            return false;
        }

        dbContext.ReportSchedules.Remove(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ReportScheduleDto ToDto(ReportScheduleEntity schedule) => new(
        schedule.ReportScheduleId,
        schedule.OrganizationId,
        schedule.BranchId,
        schedule.ReportType,
        schedule.Frequency,
        schedule.IsActive,
        schedule.NextRunUtc,
        schedule.LastRunUtc,
        schedule.CreatedAtUtc);
}
