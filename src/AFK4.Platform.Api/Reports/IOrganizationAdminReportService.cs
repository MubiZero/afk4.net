using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Reports;

public interface IOrganizationAdminReportService
{
    Task<OrganizationAdminSummaryReportDto> GetSummaryAsync(Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
    Task<OrganizationAdminShiftCashReportDto> GetShiftCashAsync(Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
    Task<OrganizationAdminRevenueReportDto> GetRevenueAsync(Guid organizationId, Guid branchId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}
