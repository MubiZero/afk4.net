using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Loyalty;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// «Приведи друга» для игрока.
///
/// Игроки у нас не регистрируются сами — аккаунт заводит клуб, — поэтому код нельзя назвать при
/// регистрации: друг называет его отдельным действием уже в приложении.
/// </summary>
internal static class PlayerReferralEndpoints
{
    public static void MapPlayerReferralEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/referral", async (
            IPlayerContextAccessor playerContextAccessor,
            IReferralService referralService,
            PlatformDbContext dbContext,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var settings = await referralService.GetActiveSettingsAsync(player.OrganizationId, ct);
            var currency = await ResolveCurrencyAsync(dbContext, player.PlayerAccountId, ct);

            if (settings is null)
            {
                // Программы нет — экран честно говорит об этом, а не показывает код, за который
                // никто не заплатит.
                return Results.Ok(new PlayerReferralDto(
                    false, null, 0, 0, 0, currency, 0, 0, 0, false, false));
            }

            var code = await referralService.EnsureCodeAsync(player.PlayerAccountId, ct);

            var invited = await dbContext.PlayerReferrals
                .AsNoTracking()
                .Where(row => row.ReferrerPlayerAccountId == player.PlayerAccountId)
                .ToListAsync(ct);

            var earned = await dbContext.LedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.PlayerAccountId == player.PlayerAccountId &&
                    entry.EntryType == LedgerEntryTypeNames.ReferralBonus)
                .SumAsync(entry => entry.AmountMinorUnits, ct);

            var mine = await dbContext.PlayerReferrals
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.InviteePlayerAccountId == player.PlayerAccountId, ct);

            var account = await dbContext.PlayerAccounts
                .AsNoTracking()
                .SingleAsync(row => row.PlayerAccountId == player.PlayerAccountId, ct);

            var windowOpen = settings.ClaimWindowDays <= 0 ||
                account.CreatedAtUtc.AddDays(settings.ClaimWindowDays) >= DateTimeOffset.UtcNow;

            return Results.Ok(new PlayerReferralDto(
                true,
                code,
                settings.ReferrerBonusMinorUnits,
                settings.InviteeBonusMinorUnits,
                settings.MinimumTopUpMinorUnits,
                currency,
                invited.Count,
                invited.Count(row => row.RewardedAtUtc is not null),
                earned,
                mine is not null,
                mine is null && windowOpen));
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/referral/claim", async (
            ClaimReferralCodeRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IReferralService referralService,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var outcome = await referralService.ClaimAsync(player.PlayerAccountId, request.Code, ct);

            // Причина отказа — машинный код: приложение переводит его само, и все причины
            // называются по-разному, потому что выход из каждой свой.
            return outcome.Succeeded
                ? Results.Ok(new { referrerDisplayName = outcome.ReferrerDisplayName })
                : Results.Conflict(new { error = outcome.ErrorCode });
        }).RequireRateLimiting("player-me");
    }

    /// <summary>
    /// Валюта журнала игрока; пустой журнал — валюта его филиала. Показать сумму бонуса без
    /// валюты нельзя, а придумывать её нельзя тем более.
    /// </summary>
    private static async Task<string> ResolveCurrencyAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken ct)
    {
        var fromLedger = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(fromLedger))
        {
            return fromLedger;
        }

        var branchCurrency = await (
            from account in dbContext.PlayerAccounts.AsNoTracking()
            join tariff in dbContext.TariffVersions.AsNoTracking()
                on account.HomeBranchId equals tariff.BranchId
            where account.PlayerAccountId == playerAccountId
            select tariff.CurrencyCode).FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(branchCurrency) ? "TJS" : branchCurrency;
    }
}
