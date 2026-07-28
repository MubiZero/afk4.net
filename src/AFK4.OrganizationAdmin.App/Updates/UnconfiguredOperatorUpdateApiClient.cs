using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Updates;

public sealed class UnconfiguredOperatorUpdateApiClient : IOperatorUpdateApiClient
{
    public Task<IReadOnlyList<UpdateRolloutStatusDto>> GetRolloutStatusesAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator update API client is not configured.");
    }

    public Task<UpdatePackageDto> RegisterPackageAsync(
        Guid branchId,
        CreateUpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator update API client is not configured.");
    }

    public Task<UpdatePackageDto> ChangePackageStateAsync(
        Guid branchId,
        Guid updatePackageId,
        UpdatePackageStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator update API client is not configured.");
    }

    public Task<UpdateRolloutDto> CreateRolloutAsync(
        Guid branchId,
        CreateUpdateRolloutRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator update API client is not configured.");
    }

    public Task<UpdateRolloutDto> ChangeRolloutStateAsync(
        Guid branchId,
        Guid updateRolloutId,
        UpdateRolloutStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator update API client is not configured.");
    }
}
