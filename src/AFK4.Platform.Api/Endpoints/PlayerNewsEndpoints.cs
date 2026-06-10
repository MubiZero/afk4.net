using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.News;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerNewsEndpoints
{
    public static void MapPlayerNewsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/news", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext db,
            INewsService news,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var homeBranchId = await db.PlayerAccounts.AsNoTracking()
                .Where(account => account.PlayerAccountId == player.PlayerAccountId)
                .Select(account => (Guid?)account.HomeBranchId)
                .FirstOrDefaultAsync(ct);
            var effectiveBranch = homeBranchId == Guid.Empty ? null : homeBranchId;

            var items = await news.ListForPlayerAsync(player.OrganizationId, effectiveBranch, ct);
            return Results.Ok(items);
        }).RequireRateLimiting("player-me");
    }
}
