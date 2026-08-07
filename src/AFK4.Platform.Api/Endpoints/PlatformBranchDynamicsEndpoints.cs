using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

public static class PlatformBranchDynamicsEndpoints
{
    public static void MapPlatformBranchDynamicsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/organizations/{organizationId:guid}/branches/{branchId:guid}/dynamics", async (
            Guid organizationId,
            Guid branchId,
            int? days,
            PlatformAdminAuthorizationService authorizationService,
            IBranchDynamicsService dynamicsService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizations);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var dynamics = await dynamicsService.GetAsync(organizationId, branchId, days ?? 0, cancellationToken);
            return dynamics is null ? Results.NotFound() : Results.Ok(dynamics);
        });
    }
}
