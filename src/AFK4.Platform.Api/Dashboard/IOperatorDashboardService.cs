using AFK4.Shared.Contracts.Dashboard;

namespace AFK4.Platform.Api.Dashboard;

public interface IOperatorDashboardService
{
    Task<OperatorDashboardSummaryDto> GetSummaryAsync(
        Guid organizationId,
        Guid branchId,
        DashboardSummaryQuery query,
        CancellationToken cancellationToken);
}
