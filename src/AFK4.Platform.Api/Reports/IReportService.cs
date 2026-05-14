using AFK4.Shared.Contracts.Reports;

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
}
