using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Evaluates active sessions once and issues warn/lock device commands:
/// a fixed session warns 5 minutes before its end and locks at the end (moving
/// to a time-up, awaiting-checkout state); an open postpaid tab warns then locks
/// when its accrued cost (time + attached unpaid POS) reaches the effective
/// credit limit. Warnings and locks are issued at most once per session.
/// </summary>
public sealed class AutoProtectionRunner(
    PlatformDbContext dbContext,
    ISessionBillingService sessionBillingService,
    IDeviceCommandDispatchService deviceCommandDispatchService,
    AutoProtectionOptions options,
    TimeProvider timeProvider)
{
    private static readonly string[] UnpaidPosStates =
    [
        PosSaleStateNames.Draft,
        PosSaleStateNames.PendingPayment
    ];

    /// <summary>Runs one pass and returns the number of sessions warned or locked.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.Sessions
            .Where(session => session.State == SessionStateNames.Active)
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return 0;
        }

        var branchIds = sessions.Select(session => session.BranchId).Distinct().ToList();
        var branchLimits = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branchIds.Contains(branch.BranchId))
            .ToDictionaryAsync(branch => branch.BranchId, branch => branch.PostpaidCreditLimitMinorUnits, cancellationToken);

        var playerIds = sessions
            .Where(session => session.PlayerAccountId is not null)
            .Select(session => session.PlayerAccountId!.Value)
            .Distinct()
            .ToList();
        var playerLimits = playerIds.Count == 0
            ? new Dictionary<Guid, long?>()
            : await dbContext.PlayerAccounts
                .AsNoTracking()
                .Where(player => playerIds.Contains(player.PlayerAccountId))
                .ToDictionaryAsync(player => player.PlayerAccountId, player => player.PostpaidCreditLimitMinorUnits, cancellationToken);

        var changedCount = 0;
        foreach (var session in sessions)
        {
            var changed = session.EndsAtUtc is { } endsAtUtc
                ? await EvaluateFixedAsync(session, endsAtUtc, now, cancellationToken)
                : session.PlayerAccountId is { } playerId
                    && await EvaluateOpenTabAsync(session, playerId, branchLimits, playerLimits, now, cancellationToken);

            if (changed)
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return changedCount;
    }

    private async Task<bool> EvaluateFixedAsync(
        SessionEntity session,
        DateTimeOffset endsAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (now >= endsAtUtc)
        {
            if (session.AutoLockedAtUtc is not null)
            {
                return false;
            }

            await DispatchAsync(session, DeviceCommandTypeNames.Lock, "auto-time-up", cancellationToken);
            session.AutoLockedAtUtc = now;
            session.UpdatedAtUtc = now;
            return true;
        }

        if (now >= endsAtUtc - options.WarnBeforeExpiry)
        {
            if (session.AutoWarnedAtUtc is not null)
            {
                return false;
            }

            await DispatchAsync(session, DeviceCommandTypeNames.Warn, "time-almost-up", cancellationToken);
            session.AutoWarnedAtUtc = now;
            session.UpdatedAtUtc = now;
            return true;
        }

        return false;
    }

    private async Task<bool> EvaluateOpenTabAsync(
        SessionEntity session,
        Guid playerId,
        IReadOnlyDictionary<Guid, long?> branchLimits,
        IReadOnlyDictionary<Guid, long?> playerLimits,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (session.AutoLockedAtUtc is not null)
        {
            return false;
        }

        // Effective limit = player override ?? branch default ?? none (unbounded).
        var playerLimit = playerLimits.TryGetValue(playerId, out var playerOverride) ? playerOverride : null;
        var branchLimit = branchLimits.TryGetValue(session.BranchId, out var branchDefault) ? branchDefault : null;
        var effectiveLimit = playerLimit ?? branchLimit;
        if (effectiveLimit is null)
        {
            return false;
        }

        var accrued = await ComputeAccruedAsync(session, now, cancellationToken);
        if (accrued < effectiveLimit.Value)
        {
            return false;
        }

        if (session.AutoWarnedAtUtc is null)
        {
            await DispatchAsync(session, DeviceCommandTypeNames.Warn, "credit-limit", cancellationToken);
            session.AutoWarnedAtUtc = now;
        }

        await DispatchAsync(session, DeviceCommandTypeNames.Lock, "credit-limit", cancellationToken);
        session.AutoLockedAtUtc = now;
        session.UpdatedAtUtc = now;
        return true;
    }

    private async Task<long> ComputeAccruedAsync(SessionEntity session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var timeCharge = await sessionBillingService.ComputeCheckoutChargeAsync(session.SessionId, now, cancellationToken);
        var accrued = timeCharge.Succeeded ? timeCharge.AmountMinorUnits : 0;

        var posTotal = await dbContext.PosSales
            .Where(sale => sale.SessionId == session.SessionId && UnpaidPosStates.Contains(sale.State))
            .SumAsync(sale => (long?)sale.TotalMinorUnits, cancellationToken) ?? 0;

        return accrued + posTotal;
    }

    private Task DispatchAsync(SessionEntity session, string type, string reason, CancellationToken cancellationToken) =>
        deviceCommandDispatchService.DispatchAsync(
            session.DeviceId,
            new CreateDeviceCommandRequest(
                Type: type,
                Payload: new Dictionary<string, string>
                {
                    ["sessionId"] = session.SessionId.ToString("D"),
                    ["reason"] = reason
                }),
            cancellationToken);
}
