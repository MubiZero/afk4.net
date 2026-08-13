using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Reservations;
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

        // Сколько будет стоить бронь по выбранному тарифу — до того, как игрок её подтвердит.
        // Считает сервер, а не приложение: минимальное оплачиваемое время и шаг округления живут в
        // биллинге, и вторая реализация в клиенте разошлась бы с настоящим списанием.
        app.MapPost("/api/me/reservations/quote", async (
            ReservationQuoteRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            if (request.StartsAtUtc >= request.EndsAtUtc)
            {
                return Results.BadRequest(new { error = "invalid_duration" });
            }

            var now = timeProvider.GetUtcNow();
            var row = await dbContext.TariffVersions.AsNoTracking()
                .Where(version =>
                    version.TariffVersionId == request.TariffVersionId &&
                    version.OrganizationId == player.OrganizationId &&
                    version.EffectiveFromUtc <= now &&
                    (version.RetiredAtUtc == null || version.RetiredAtUtc > now))
                .Join(
                    dbContext.Tariffs.AsNoTracking(),
                    version => version.TariffId,
                    tariff => tariff.TariffId,
                    (version, tariff) => new { Version = version, tariff.Name })
                .FirstOrDefaultAsync(ct);

            // Снятая с публикации или чужая версия — не повод посчитать по «похожему» тарифу:
            // приложение должно перечитать прайс, а не показать цену, которой уже нет.
            if (row is null) return Results.NotFound(new { error = "invalid_tariff" });

            var requestedMinutes = (int)Math.Round((request.EndsAtUtc - request.StartsAtUtc).TotalMinutes);
            var charge = TariffBilling.ComputeForMinutes(
                requestedMinutes,
                new TariffPricing(
                    row.Version.PricePerMinuteMinorUnits,
                    row.Version.MinimumBillableMinutes,
                    row.Version.RoundingIncrementMinutes,
                    row.Version.CurrencyCode));
            if (charge is null) return Results.BadRequest(new { error = "invalid_duration" });

            return Results.Ok(new ReservationQuoteDto(
                row.Version.TariffVersionId,
                row.Name,
                requestedMinutes,
                charge.BillableMinutes,
                charge.AmountMinorUnits,
                charge.CurrencyCode));
        }).RequireRateLimiting("player-me");
    }

    private static Task<bool> BranchInOrgAsync(
        PlatformDbContext db, Guid branchId, Guid orgId, CancellationToken ct) =>
        db.Branches.AsNoTracking()
            .AnyAsync(b => b.BranchId == branchId && b.OrganizationId == orgId, ct);
}
