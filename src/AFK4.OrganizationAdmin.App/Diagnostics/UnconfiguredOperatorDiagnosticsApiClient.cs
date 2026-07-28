using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.OrganizationAdmin.App.Diagnostics;

public sealed class UnconfiguredOperatorDiagnosticsApiClient : IOperatorDiagnosticsApiClient
{
    public Task<BranchDiagnosticsDto> GetDiagnosticsAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator diagnostics API client is not configured.");
    }
}
