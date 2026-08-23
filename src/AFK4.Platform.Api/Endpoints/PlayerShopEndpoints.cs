using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Commerce;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerShopEndpoints
{
    public static void MapPlayerShopEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/shop/catalog", async (
            IPlayerContextAccessor playerContextAccessor,
            IOrganizationEntitlements entitlements,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var featureDenial = await entitlements.RequireAsync(
                player.OrganizationId, PlatformFeatureNames.PlayerShop, ct);
            if (featureDenial is not null) return featureDenial;

            var session = await db.Sessions.AsNoTracking()
                .Where(s => s.PlayerAccountId == player.PlayerAccountId
                    && (s.State == SessionStateNames.Active || s.State == SessionStateNames.Paused))
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync(ct);
            if (session is null) return Results.Ok(Array.Empty<ShopCatalogItemDto>());

            var products = await db.PosProducts.AsNoTracking()
                .Where(p => p.BranchId == session.BranchId && p.IsActive && p.AvailableInShell)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
            var productIds = products.Select(p => p.ProductId).ToList();
            var stock = (await db.StockMovements.AsNoTracking()
                    .Where(m => m.BranchId == session.BranchId && productIds.Contains(m.ProductId))
                    .ToListAsync(ct))
                .GroupBy(m => m.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.QuantityDelta));

            var catalog = products
                .Where(p => !p.TrackStock || p.AllowNegativeStock || stock.GetValueOrDefault(p.ProductId) > 0)
                .Select(p => new ShopCatalogItemDto(
                    p.ProductId, p.Name, p.Sku,
                    new MoneyDto(p.CurrencyCode, p.PriceMinorUnits),
                    stock.GetValueOrDefault(p.ProductId)))
                .ToList();
            return Results.Ok(catalog);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/shop/orders", async (
            PlaceShopOrderRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IShopCommerceCoordinator commerceCoordinator,
            IOrganizationEntitlements entitlements,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var featureDenial = await entitlements.RequireAsync(
                player.OrganizationId, PlatformFeatureNames.PlayerShop, ct);
            if (featureDenial is not null) return featureDenial;

            var result = await commerceCoordinator.PlaceAsync(player.PlayerAccountId, request, ct);
            return ToHttpResult(result);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/shop/orders", async (
            IPlayerContextAccessor playerContextAccessor,
            IShopOrderService shopOrderService,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            return Results.Ok(await shopOrderService.ListForPlayerAsync(player.PlayerAccountId, ct));
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/shop/orders/{orderId:guid}/cancel", async (
            Guid orderId,
            IPlayerContextAccessor playerContextAccessor,
            IShopCommerceCoordinator commerceCoordinator,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            var result = await commerceCoordinator.CancelByPlayerAsync(player.PlayerAccountId, orderId, ct);
            return ToHttpResult(result);
        }).RequireRateLimiting("player-me").WorksWhenNetworkBanned();
    }

    private static IResult ToHttpResult(ShopOrderActionResult result)
    {
        if (result.Succeeded) return Results.Ok(result.Order);
        if (result.NotFound) return Results.NotFound();
        if (result.Conflict) return Results.Conflict(new { error = result.ErrorCode, currentVersion = result.CurrentVersion });
        return Results.Conflict(new { error = result.ErrorCode });
    }
}
