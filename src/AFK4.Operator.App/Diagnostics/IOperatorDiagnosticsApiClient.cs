using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.Operator.App.Diagnostics;

public interface IOperatorDiagnosticsApiClient
{
    Task<BranchDiagnosticsDto> GetDiagnosticsAsync(
        Guid branchId,
        CancellationToken cancellationToken);
}
