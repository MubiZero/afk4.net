using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerCatalogEndpoints
{
    public static void MapPlayerCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/branches/{branchId:guid}/tariffs", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IOperatorReferenceDataService referenceData,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();
            return Results.Ok(await referenceData.GetTariffOptionsAsync(player.OrganizationId, branchId, ct));
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/branches/{branchId:guid}/packages", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IOperatorReferenceDataService referenceData,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();
            return Results.Ok(await referenceData.GetPackageOptionsAsync(player.OrganizationId, branchId, ct));
        }).RequireRateLimiting("player-me");
    }

    private static Task<bool> BranchInOrgAsync(
        PlatformDbContext db, Guid branchId, Guid orgId, CancellationToken ct) =>
        db.Branches.AsNoTracking()
            .AnyAsync(b => b.BranchId == branchId && b.OrganizationId == orgId, ct);
}
