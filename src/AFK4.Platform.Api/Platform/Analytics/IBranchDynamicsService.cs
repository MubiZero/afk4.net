using AFK4.Shared.Contracts.Platform.Analytics;

namespace AFK4.Platform.Api.Platform.Analytics;

public interface IBranchDynamicsService
{
    /// <summary>Возвращает <c>null</c>, если такого филиала у этой организации нет.</summary>
    Task<BranchDynamicsDto?> GetAsync(Guid organizationId, Guid branchId, int days, CancellationToken cancellationToken);
}
