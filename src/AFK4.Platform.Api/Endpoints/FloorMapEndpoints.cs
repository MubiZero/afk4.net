using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Endpoints;

internal static class FloorMapEndpoints
{
    public static void MapFloorMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/floor-map", async (
            Guid branchId,
            HttpContext httpContext,
            IFloorMapReadService floorMapReadService,
            StaffAuthorizationService authorizationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewFloorMap,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await floorMapReadService.GetFloorMapAsync(branchId, cancellationToken);

            if (result is null)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers.ETag = result.ETag;
            return Results.Ok(result.FloorMap);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewFloorMap);

    }
}
