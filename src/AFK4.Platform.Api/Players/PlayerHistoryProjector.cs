using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Common;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Common;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

// Player-scoped history reads: past visits (ended sessions) and standalone POS
// purchases. Totals for a visit are read from the session_checkout receipt the
// counter-loop writes — the portal renders, never re-computes the charge.
public static class PlayerHistoryProjector
{
    private const int PageSize = 20;

    public static async Task<CursorPage<PlayerVisitDto>> GetVisitsAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.PlayerAccountId == playerAccountId &&
                session.State == SessionStateNames.Ended &&
                session.EndedAtUtc != null);

        // EF Core InMemory does not translate Guid.CompareTo inside LINQ Where.
        // Filter by timestamp boundary only in SQL/InMemory, then apply the precise
        // (EndedAtUtc, SessionId) tie-break in memory after materializing.
        DateTimeOffset afterTs = default;
        Guid afterId = default;
        bool hasCursor = CursorToken.TryDecode(cursor, out afterTs, out afterId);

        if (hasCursor)
        {
            query = query.Where(session => session.EndedAtUtc <= afterTs);
        }

        // Fetch a larger window so we can apply the in-memory tie-break and still
        // have PageSize+1 candidates to determine hasMore.
        var windowSize = hasCursor ? (PageSize + 1) * 2 : PageSize + 1;
        var candidates = await query
            .OrderByDescending(session => session.EndedAtUtc)
            .ThenByDescending(session => session.SessionId)
            .Take(windowSize)
            .ToListAsync(cancellationToken);

        // Apply the (EndedAtUtc DESC, SessionId DESC) keyset tie-break in memory.
        List<SessionEntity> sessions;
        if (hasCursor)
        {
            sessions = candidates
                .Where(session =>
                    session.EndedAtUtc < afterTs ||
                    (session.EndedAtUtc == afterTs && session.SessionId.CompareTo(afterId) < 0))
                .Take(PageSize + 1)
                .ToList();
        }
        else
        {
            sessions = candidates.Take(PageSize + 1).ToList();
        }

        var hasMore = sessions.Count > PageSize;
        if (hasMore)
        {
            sessions.RemoveAt(sessions.Count - 1);
        }

        var sessionIds = sessions.Select(s => s.SessionId).ToList();
        var seatIds = sessions.Select(s => s.SeatId).Distinct().ToList();

        var seatNames = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seatIds.Contains(seat.SeatId))
            .ToDictionaryAsync(seat => seat.SeatId, seat => seat.Name, cancellationToken);

        var receipts = await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.SessionId != null && sessionIds.Contains(receipt.SessionId.Value))
            .ToListAsync(cancellationToken);
        var receiptBySession = receipts
            .GroupBy(receipt => receipt.SessionId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var posTotals = await dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.SessionId != null && sessionIds.Contains(sale.SessionId.Value))
            .GroupBy(sale => sale.SessionId!.Value)
            .Select(group => new { SessionId = group.Key, Total = group.Sum(sale => sale.TotalMinorUnits) })
            .ToDictionaryAsync(row => row.SessionId, row => row.Total, cancellationToken);

        var items = new List<PlayerVisitDto>(sessions.Count);
        foreach (var session in sessions)
        {
            var posTotal = posTotals.GetValueOrDefault(session.SessionId, 0);
            var hasReceipt = receiptBySession.TryGetValue(session.SessionId, out var receipt);
            var grandTotal = hasReceipt ? receipt!.TotalMinorUnits : posTotal;
            var timeCharge = grandTotal - posTotal;
            var currency = hasReceipt ? receipt!.CurrencyCode : "TJS";

            items.Add(new PlayerVisitDto(
                session.SessionId,
                session.SeatId,
                seatNames.GetValueOrDefault(session.SeatId, string.Empty),
                session.StartedAtUtc ?? session.RequestedAtUtc,
                session.EndedAtUtc,
                timeCharge,
                posTotal,
                grandTotal,
                currency,
                hasReceipt));
        }

        string? nextCursor = hasMore && items.Count > 0
            ? CursorToken.Encode(items[^1].EndedAtUtc!.Value, items[^1].SessionId)
            : null;

        return new CursorPage<PlayerVisitDto>(items, nextCursor);
    }
}
