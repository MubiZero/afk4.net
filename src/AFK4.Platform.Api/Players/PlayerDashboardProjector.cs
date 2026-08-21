using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

// Builds the player dashboard: wallet/debt (reusing LedgerBalanceProjector) plus
// the player's active session with its live accrued cost (open) or remaining
// time (fixed). The accrued-cost math reuses the shared TariffBilling primitive,
// so the portal and the operator floor map never disagree.
public static class PlayerDashboardProjector
{
    public static async Task<PlayerDashboardDto> GetDashboardAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
            dbContext, playerAccountId, cancellationToken);

        var walletBalance = wallet?.WalletBalance ?? new MoneyDto("TJS", 0);
        var heldBalance = wallet?.HeldBalance ?? new MoneyDto("TJS", 0);
        var debtBalance = wallet?.DebtBalance ?? new MoneyDto("TJS", 0);

        var session = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.PlayerAccountId == playerAccountId &&
                    candidate.State == SessionStateNames.Active,
                cancellationToken);

        ActiveSessionDto? activeSession = null;
        if (session is not null)
        {
            activeSession = await BuildActiveSessionAsync(dbContext, session, now, cancellationToken);
        }

        return new PlayerDashboardDto(walletBalance, heldBalance, debtBalance, activeSession);
    }

    private static async Task<ActiveSessionDto> BuildActiveSessionAsync(
        PlatformDbContext dbContext,
        SessionEntity session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seatName = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.SeatId == session.SeatId)
            .Select(seat => seat.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var startedAtUtc = session.StartedAtUtc ?? now;
        var currencyCode = "TJS";

        // Fixed session: expose remaining time, no accrued cost.
        if (session.EndsAtUtc is not null)
        {
            var remaining = (int)Math.Max(0, (session.EndsAtUtc.Value - now).TotalSeconds);
            return new ActiveSessionDto(
                session.SessionId, session.SeatId, seatName, startedAtUtc,
                "fixed", remaining, null, currencyCode);
        }

        // Open tab: count-up accrued cost via the shared tariff primitive.
        long? accrued = null;
        if (Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
        {
            var version = await dbContext.TariffVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.TariffVersionId == tariffVersionId, cancellationToken);
            if (version is not null)
            {
                currencyCode = version.CurrencyCode;
                var pricing = new TariffPricing(
                    version.PricePerMinuteMinorUnits,
                    version.MinimumBillableMinutes,
                    version.RoundingIncrementMinutes,
                    version.CurrencyCode);
                var computation = TariffBilling.ComputeForElapsed(now - startedAtUtc, pricing);
                accrued = computation?.AmountMinorUnits;
            }
        }

        return new ActiveSessionDto(
            session.SessionId, session.SeatId, seatName, startedAtUtc,
            "open", null, accrued, currencyCode);
    }
}
