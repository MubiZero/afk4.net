using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Updates;

public interface IOperatorUpdateApiClient
{
    Task<IReadOnlyList<UpdateRolloutStatusDto>> GetRolloutStatusesAsync(
        Guid branchId,
        CancellationToken cancellationToken);

    Task<UpdatePackageDto> RegisterPackageAsync(
        Guid branchId,
        CreateUpdatePackageRequest request,
        CancellationToken cancellationToken);

    Task<UpdatePackageDto> ChangePackageStateAsync(
        Guid branchId,
        Guid updatePackageId,
        UpdatePackageStateChangeRequest request,
        CancellationToken cancellationToken);

    Task<UpdateRolloutDto> CreateRolloutAsync(
        Guid branchId,
        CreateUpdateRolloutRequest request,
        CancellationToken cancellationToken);

    Task<UpdateRolloutDto> ChangeRolloutStateAsync(
        Guid branchId,
        Guid updateRolloutId,
        UpdateRolloutStateChangeRequest request,
        CancellationToken cancellationToken);
}
