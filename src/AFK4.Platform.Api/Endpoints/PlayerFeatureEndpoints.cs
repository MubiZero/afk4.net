using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerFeatureEndpoints
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
}
