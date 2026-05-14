using AFK4.Shared.Contracts.Audit;

namespace AFK4.Operator.App.Audit;

public sealed class UnconfiguredOperatorAuditApiClient : IOperatorAuditApiClient
{
    public Task<AuditSearchResultDto> SearchAsync(
        OperatorAuditSearchRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator audit API client is not configured.");
    }
}
