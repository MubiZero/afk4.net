using AFK4.Shared.Contracts.Updates;

namespace AFK4.Platform.Api.Updates;

public interface IUpdateService
{
    Task<UpdateServiceResult<UpdatePackageDto>> RegisterPackageAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateUpdatePackageRequest request,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<UpdateRolloutDto>> CreateRolloutAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateUpdateRolloutRequest request,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<UpdateRolloutDto>> GetRolloutAsync(
        Guid organizationId,
        Guid branchId,
        Guid rolloutId,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<DeviceUpdateCheckResponse>> CheckForUpdatesAsync(
        DeviceUpdateCheckRequest request,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<DeviceUpdateStatusResultDto>> ReportStatusAsync(
        DeviceUpdateStatusReportRequest request,
        CancellationToken cancellationToken);
}
