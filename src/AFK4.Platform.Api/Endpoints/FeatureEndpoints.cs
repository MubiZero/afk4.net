using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Endpoints;

internal static class FeatureEndpoints
{
    public static void MapPlayerFeatureEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/features", async (
            IPlayerContextAccessor playerContextAccessor,
            IOrganizationEntitlements entitlements,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var enabled = await entitlements.ListEnabledAsync(player.OrganizationId, cancellationToken);
            return Results.Ok(new EnabledFeaturesDto(enabled));
        }).RequireRateLimiting("player-me");
    }

    // Staff-facing counterpart of /api/me/features: the Operator app authenticates as a staff
    // member, not a player, so it cannot use the player route. No permission gate — any
    // authenticated staff member of the organization may see which features are on, same bar as
    // the player route. The route's organizationId is untrusted input; it is checked against the
    // org id carried by the staff token, and a mismatch is refused (403), never served from
    // another organization's data (IDOR).
    public static void MapOrganizationFeatureEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("features", async (
            Guid organizationId,
            IStaffContextAccessor staffContextAccessor,
            IOrganizationEntitlements entitlements,
            CancellationToken cancellationToken) =>
        {
            var staffContext = staffContextAccessor.Current;
            if (staffContext is null)
            {
                return Results.Unauthorized();
            }

            if (organizationId != staffContext.OrganizationId)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var enabled = await entitlements.ListEnabledAsync(organizationId, cancellationToken);
            return Results.Ok(new EnabledFeaturesDto(enabled));
        });
    }
}
