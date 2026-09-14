using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Reports;

/// <summary>
/// Manages branch report schedules picked up by the scheduled-report runner. Organization isolation is
/// enforced by always scoping to (organizationId, branchId).
/// </summary>
public interface IReportScheduleService
{
    /// <summary>
    /// Есть ли у филиала уже такая же рассылка. Две одинаковые означают два одинаковых письма
    /// владельцу каждый период — вреда счетам нет, но чинить это владелец будет удалением, а
    /// предотвращает стойка.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid organizationId,
        Guid branchId,
        string reportType,
        string frequency,
        CancellationToken cancellationToken);

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
