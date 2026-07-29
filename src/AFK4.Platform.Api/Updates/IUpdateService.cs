using AFK4.Shared.Contracts.Updates;

namespace AFK4.Platform.Api.Updates;

public interface IUpdateService
{
    Task<UpdateServiceResult<UpdateRolloutDto>> GetRolloutAsync(
        Guid organizationId,
        Guid branchId,
        Guid rolloutId,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<IReadOnlyList<UpdateRolloutStatusDto>>> ListRolloutStatusesAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<DeviceUpdateCheckResponse>> CheckForUpdatesAsync(
        DeviceUpdateCheckRequest request,
        CancellationToken cancellationToken);

    Task<UpdateServiceResult<DeviceUpdateStatusResultDto>> ReportStatusAsync(
        DeviceUpdateStatusReportRequest request,
        CancellationToken cancellationToken);
}
