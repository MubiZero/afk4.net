using AFK4.Platform.Api.Ads;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// «Реклама на ваших ПК» (спека рекламы, §8.4): клуб — тоже распространитель рекламы, поэтому видит,
/// какая реклама платформы идёт и шла на его ПК. Смотреть — тем, кто видит подписку клуба.
/// </summary>
internal static class ClubAdEndpoints
{
    public static void MapClubAdEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("platform-ads", async (
            Guid organizationId, StaffAuthorizationService authorizationService, PlatformDbContext db,
            IOrganizationFeatureSnapshot features, IOptions<InstallOptions> install, TimeProvider clock, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed || organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var adsEnabled = (await features.GetEnabledAsync(organizationId, ct)).Contains(PlatformFeatureNames.PlatformAds);
            return Results.Ok(await ClubAds.ListAsync(db, organizationId, adsEnabled, clock.GetUtcNow(), install.Value.ApiBaseUrl, ct));
        });
    }
}
