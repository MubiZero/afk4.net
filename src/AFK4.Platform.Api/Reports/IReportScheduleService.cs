using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Reports;

/// <summary>
/// Manages branch report schedules picked up by the scheduled-report runner. Organization isolation is
/// enforced by always scoping to (organizationId, branchId).
/// </summary>
public interface IReportScheduleService
{
    Task<ReportScheduleDto> CreateAsync(
        Guid organizationId,
        Guid branchId,
        Guid createdByStaffUserId,
        string reportType,
        string frequency,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportScheduleDto>> ListAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        Guid organizationId,
        Guid branchId,
        Guid reportScheduleId,
        CancellationToken cancellationToken);
}
