using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Tournaments;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// События клуба глазами игрока: расписание зала, запись и снятие. Черновиков и списка
/// участников здесь нет — первое клуба ещё не касается никого, второе касается только стойки.
/// </summary>
internal static class PlayerTournamentEndpoints
{
    public static void MapPlayerTournamentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/branches/{branchId:guid}/tournaments", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            IOrganizationEntitlements entitlements,
            ITournamentService tournaments,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var featureDenial = await entitlements.RequireAsync(
                player.OrganizationId, PlatformFeatureNames.Tournaments, ct);
            if (featureDenial is not null) return featureDenial;

            var branchInOrg = await db.Branches.AsNoTracking()
                .AnyAsync(branch => branch.BranchId == branchId
                    && branch.OrganizationId == player.OrganizationId, ct);
            if (!branchInOrg) return Results.NotFound();

            var items = await tournaments.ListForPlayerAsync(
                player.OrganizationId, branchId, player.PlayerAccountId, ct);
            return Results.Ok(items);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/tournaments/{tournamentId:guid}/registration", async (
            Guid tournamentId,
            IPlayerContextAccessor playerContextAccessor,
            IOrganizationEntitlements entitlements,
            ITournamentService tournaments,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var featureDenial = await entitlements.RequireAsync(
                player.OrganizationId, PlatformFeatureNames.Tournaments, ct);
            if (featureDenial is not null) return featureDenial;

            var result = await tournaments.RegisterAsync(
                player.OrganizationId, player.PlayerAccountId, tournamentId, ct);
            if (result.NotFound) return Results.NotFound();
            // Отказы здесь все про состояние — мест нет, уже началось, денег не хватает, — а не
            // про кривой запрос. Причина уезжает кодом: фразу игроку собирает приложение.
            if (!result.Succeeded) return Results.Conflict(new { error = result.Error });
            return Results.Ok(result.Value);
        }).RequireRateLimiting("player-me");

        app.MapDelete("/api/me/tournaments/{tournamentId:guid}/registration", async (
            Guid tournamentId,
            IPlayerContextAccessor playerContextAccessor,
            IOrganizationEntitlements entitlements,
            ITournamentService tournaments,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var featureDenial = await entitlements.RequireAsync(
                player.OrganizationId, PlatformFeatureNames.Tournaments, ct);
            if (featureDenial is not null) return featureDenial;

            var result = await tournaments.CancelRegistrationAsync(
                player.OrganizationId, player.PlayerAccountId, tournamentId, ct);
            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.Conflict(new { error = result.Error });
            return Results.Ok(result.Value);
        }).RequireRateLimiting("player-me");
    }
}
