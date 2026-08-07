using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformDebtEndpoints
{
    public static void MapPlatformDebtEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/debt", async (
            PlatformAdminAuthorizationService authorizationService,
            IDebtOverviewService debtOverviewService,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var rows = await debtOverviewService.GetAsync(timeProvider.GetUtcNow(), cancellationToken);
            return Results.Ok(rows);
        });
    }
}
