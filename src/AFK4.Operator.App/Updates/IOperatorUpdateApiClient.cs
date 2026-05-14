using AFK4.Shared.Contracts.Updates;

namespace AFK4.Operator.App.Updates;

public interface IOperatorUpdateApiClient
{
    Task<IReadOnlyList<UpdateRolloutStatusDto>> GetRolloutStatusesAsync(
        Guid branchId,
        CancellationToken cancellationToken);
}
