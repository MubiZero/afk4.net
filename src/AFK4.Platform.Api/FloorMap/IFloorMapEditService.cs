using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public interface IFloorMapEditService
{
    Task<FloorMapBulkUpdateResult> BulkUpdateAsync(
        Guid organizationId,
        Guid branchId,
        string? ifMatch,
        FloorMapBulkUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed record FloorMapBulkUpdateResult(
    FloorMapBulkUpdateStatus Status,
    string? Error,
    FloorMapBulkUpdateResponse? Response,
    string? CurrentETag);

public enum FloorMapBulkUpdateStatus
{
    Success,
    BadRequest,
    PreconditionRequired,
    PreconditionFailed,
    NotFound
}
