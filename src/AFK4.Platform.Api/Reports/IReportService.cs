using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Platform.Api.Reports;

public interface IReportService
{
    Task<ShiftReportResultDto> GetShiftReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken);

    Task<SalesReportResultDto> GetSalesReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken);

    Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken);

    Task<CashOperationReportResultDto> GetCashOperationReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken);

    Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
        Guid organizationId,
        Guid branchId,
        ReportSearchQuery query,
        CancellationToken cancellationToken);

    // Anti-fraud §5.6: the owner's per-actor daily high-risk digest for a single branch-day.
    Task<OwnerDailySummaryResultDto> GetOwnerDailySummaryAsync(
        Guid organizationId,
        Guid branchId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task<ShiftRevenueListDto> GetShiftRevenueAsync(
        Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken);

    Task<ShiftRevenueDto?> GetCurrentShiftRevenueAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken);
}
