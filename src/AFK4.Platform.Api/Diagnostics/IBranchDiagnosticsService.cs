using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.Platform.Api.Diagnostics;

public interface IBranchDiagnosticsService
{
    Task<BranchDiagnosticsDto> GetAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);
}
