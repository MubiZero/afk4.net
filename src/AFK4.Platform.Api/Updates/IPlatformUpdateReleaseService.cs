using AFK4.Shared.Contracts.Platform.Updates;

namespace AFK4.Platform.Api.Updates;

public interface IPlatformUpdateReleaseService
{
    Task<UpdateServiceResult<PlatformUpdatePackageDto>> RegisterPackageAsync(
        Guid actorPlatformAdminUserId,
        CreatePlatformUpdatePackageRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformUpdatePackageDto>> ListPackagesAsync(CancellationToken cancellationToken);

    Task<UpdateServiceResult<PlatformUpdatePackageDto>> ChangePackageStateAsync(
        Guid actorPlatformAdminUserId,
        Guid packageId,
        ChangePlatformUpdatePackageStateRequest request,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<PlatformUpdateRolloutDto>> CreateRolloutAsync(
        Guid actorPlatformAdminUserId,
        CreatePlatformUpdateRolloutRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformUpdateRolloutDto>> ListRolloutsAsync(CancellationToken cancellationToken);

    Task<UpdateServiceResult<PlatformUpdateRolloutDto>> ChangeRolloutStateAsync(
        Guid rolloutId,
        ChangePlatformUpdateRolloutStateRequest request,
        CancellationToken cancellationToken);
}
