using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Commerce;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

public sealed record ShopOrderActionRequest(int? ExpectedVersion);

internal static class ShopOrderEndpoints
{
    public static void MapShopOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branches/{branchId:guid}/shop/orders", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IShopOrderService shopOrderService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageShopOrders, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await shopOrderService.ListQueueAsync(branchId, cancellationToken));
        });

        MapTransition(app, "accept", AuditActionNames.AcceptShopOrder,
            (svc, branchId, orderId, staffUserId, version, ct) => svc.AcceptAsync(branchId, orderId, staffUserId, version, ct));
        MapTransition(app, "deliver", AuditActionNames.DeliverShopOrder,
            (svc, branchId, orderId, staffUserId, version, ct) => svc.DeliverAsync(branchId, orderId, staffUserId, version, ct));
        MapCancel(app);
    }

    private static void MapTransition(
        WebApplication app,
        string verb,
        string auditAction,
        Func<IShopOrderService, Guid, Guid, Guid, int?, CancellationToken, Task<ShopOrderActionResult>> action)
    {
        app.MapPost($"/api/branches/{{branchId:guid}}/shop/orders/{{orderId:guid}}/{verb}", async (
            Guid branchId,
            Guid orderId,
            ShopOrderActionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShopOrderService shopOrderService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageShopOrders, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await action(
                shopOrderService,
                branchId,
                orderId,
                authorization.StaffContext!.StaffUserId,
                request.ExpectedVersion,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                auditAction,
                "ShopOrder",
                orderId.ToString("D"),
                result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                new { result.ErrorCode },
                cancellationToken);

            if (result.Succeeded)
            {
                return Results.Ok(result.Order);
            }

            if (result.NotFound)
            {
                return Results.NotFound();
            }

            if (result.Conflict)
            {
                return Results.Conflict(new { error = result.ErrorCode, currentVersion = result.CurrentVersion });
            }

            return Results.Conflict(new { error = result.ErrorCode });
        });
    }

    private static void MapCancel(WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/shop/orders/{orderId:guid}/cancel", async (
            Guid branchId,
            Guid orderId,
            ShopOrderActionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShopCommerceCoordinator commerceCoordinator,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageShopOrders, cancellationToken);

            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await commerceCoordinator.CancelByOperatorAsync(
                branchId,
                orderId,
                authorization.StaffContext!.StaffUserId,
                request.ExpectedVersion,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CancelShopOrder,
                "ShopOrder",
                orderId.ToString("D"),
                result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                new { result.ErrorCode },
                cancellationToken);

            if (result.Succeeded) return Results.Ok(result.Order);
            if (result.NotFound) return Results.NotFound();
            if (result.Conflict)
            {
                return Results.Conflict(new { error = result.ErrorCode, currentVersion = result.CurrentVersion });
            }

            return Results.Conflict(new { error = result.ErrorCode });
        });
    }
}
