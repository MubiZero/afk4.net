using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Endpoints;

internal static class FloorMapEndpoints
{
    public static void MapFloorMapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/floor-map", async (
            Guid branchId,
            HttpContext httpContext,
            EfFloorMapReadService floorMapReadService,
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
            return Results.Ok(VisibleTo(authorization.StaffContext!, branchId, result.FloorMap));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewFloorMap);

    }

    /// <summary>
    /// Карта — вход техника в приложение: по ней он ищет сломанный ПК. Но право «видеть карту» не
    /// давало права на людей и деньги, а ответ нёс имя каждого играющего гостя, тариф и набежавшую
    /// сумму. Теперь их видит только тот, у кого в этом филиале есть права на игроков и на счёт.
    /// </summary>
    internal static FloorMapDto VisibleTo(StaffContext staff, Guid branchId, FloorMapDto floorMap)
    {
        var seesPlayers = staff.HasBranchPermission(branchId, OrganizationPermissionNames.ViewPlayers);
        var seesBilling = staff.HasBranchPermission(branchId, OrganizationPermissionNames.ViewBilling);
        if (seesPlayers && seesBilling)
        {
            return floorMap;
        }

        return floorMap with
        {
            Seats = floorMap.Seats.Select(seat => seat with
            {
                PlayerDisplayName = seesPlayers ? seat.PlayerDisplayName : null,
                TariffName = seesBilling ? seat.TariffName : null,
                AccruedCostMinorUnits = seesBilling ? seat.AccruedCostMinorUnits : null
            }).ToList()
        };
    }
}
