using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformHealthEndpoints
{
    public static void MapPlatformHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/health/overview", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformHealthOverviewService overviewService,
            CancellationToken cancellationToken) =>
        {
            // Право проверяется ДО обращения к данным — не после.
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewPlatformHealth);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await overviewService.GetOverviewAsync(cancellationToken));
        });
    }
}
