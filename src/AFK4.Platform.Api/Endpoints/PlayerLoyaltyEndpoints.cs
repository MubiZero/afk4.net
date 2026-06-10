using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerLoyaltyEndpoints
{
    public static void MapPlayerLoyaltyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/loyalty", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var settings = await db.OrganizationLoyaltySettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.OrganizationId == player.OrganizationId, ct);

            var cashbackQuery = db.LedgerEntries.AsNoTracking()
                .Where(e => e.PlayerAccountId == player.PlayerAccountId && e.EntryType == LedgerEntryTypeNames.Cashback);

            var totalMinor = await cashbackQuery.SumAsync(e => (long?)e.AmountMinorUnits, ct) ?? 0;

            var entries = await cashbackQuery
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(20)
                .ToListAsync(ct);

            var currency = entries.Count > 0 ? entries[0].CurrencyCode : "TJS";
            var recent = entries
                .Select(e => new CashbackEntryDto(e.AmountMinorUnits, e.CurrencyCode, e.Reason, e.CreatedAtUtc))
                .ToList();

            return Results.Ok(new PlayerLoyaltyDto(
                settings?.TopUpEnabled ?? false,
                settings?.TopUpPercentBasisPoints ?? 0,
                settings?.ShopEnabled ?? false,
                settings?.ShopPercentBasisPoints ?? 0,
                new MoneyDto(currency, totalMinor),
                recent));
        }).RequireRateLimiting("player-me");
    }
}
