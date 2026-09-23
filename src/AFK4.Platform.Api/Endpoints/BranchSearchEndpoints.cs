using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Search;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class BranchSearchEndpoints
{
    // Ровно столько, сколько помещается в палитру по каждому виду: список, в котором надо
    // листать, перестаёт быть быстрым ответом на набранное.
    private const int DefaultPerKindLimit = 5;

    public static void MapBranchSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/search", async (
            Guid branchId,
            string? query,
            int? limit,
            StaffAuthorizationService authorizationService,
            IBranchSearchService searchService,
            CancellationToken cancellationToken) =>
        {
            // Пускаем всякого, у кого есть хоть один из разделов: кассиру без карты зала поиск
            // чека или заказа нужен ровно так же, как оператору — поиск места.
            var authorization = await authorizationService.RequireBranchAnyPermissionAsync(
                branchId,
                [
                    OrganizationPermissionNames.ViewFloorMap,
                    OrganizationPermissionNames.ViewPlayers,
                    OrganizationPermissionNames.ViewReservations,
                    OrganizationPermissionNames.ViewReceipt,
                    OrganizationPermissionNames.ServeShopOrders
                ],
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Искать человек может только то, что ему и так видно: иначе палитра стала бы
            // обходом прав — «смотреть нельзя, а найти можно».
            var staffContext = authorization.StaffContext!;
            var scope = new BranchSearchScope(
                Seats: staffContext.HasBranchPermission(branchId, OrganizationPermissionNames.ViewFloorMap),
                Players: staffContext.HasBranchPermission(branchId, OrganizationPermissionNames.ViewPlayers),
                Reservations: staffContext.HasBranchPermission(branchId, OrganizationPermissionNames.ViewReservations),
                Receipts: staffContext.HasBranchPermission(branchId, OrganizationPermissionNames.ViewReceipt),
                // Заказы — ровно тем, кому открыта лента заказов на стойке: то же право, что сервер
                // спрашивает на очередь и на «Принять/Выдать».
                Orders: staffContext.HasBranchPermission(branchId, OrganizationPermissionNames.ServeShopOrders));

            var results = await searchService.SearchAsync(
                staffContext.OrganizationId,
                branchId,
                query,
                scope,
                limit ?? DefaultPerKindLimit,
                cancellationToken);

            return Results.Ok(results);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewFloorMap);
    }
}
