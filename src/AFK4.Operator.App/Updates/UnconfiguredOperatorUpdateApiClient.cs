using AFK4.Shared.Contracts.Updates;

namespace AFK4.Operator.App.Updates;

public sealed class UnconfiguredOperatorUpdateApiClient : IOperatorUpdateApiClient
{
    public Task<IReadOnlyList<UpdateRolloutStatusDto>> GetRolloutStatusesAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator update API client is not configured.");
    }
}
