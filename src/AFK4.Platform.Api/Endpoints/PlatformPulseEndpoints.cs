using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Pulse;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformPulseEndpoints
{
    public static void MapPlatformPulseEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/pulse", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformPulseService pulseService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizations);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var pulse = await pulseService.GetPulseAsync(cancellationToken);
            return Results.Ok(pulse);
        });
    }
}
