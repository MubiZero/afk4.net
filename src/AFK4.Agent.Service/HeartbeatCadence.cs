using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service;

/// <summary>
/// Decides how long to wait before the next heartbeat (spec §6.2). Normally the backend-provided
/// interval. But when the cached lease is within <see cref="RefreshThreshold"/> of expiry (or already
/// past it, i.e. inside the offline grace window) AND the last heartbeat failed, it escalates to a
/// tight, jittered retry — maximising the chance of catching a brief connectivity window to refresh the
/// lease before the customer is ever locked out. Jitter avoids a thundering herd of agents hammering a
/// recovering backend. Pure: the caller supplies the clock and the jitter fraction.
/// </summary>
public static class HeartbeatCadence
{
    public static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan EscalatedBase = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan EscalatedJitterMax = TimeSpan.FromSeconds(1);

    public static TimeSpan NextDelay(
        bool lastHeartbeatSucceeded,
        SessionLeaseDto? lease,
        int normalIntervalSeconds,
        DateTimeOffset nowUtc,
        double jitterFraction)
    {
        if (!lastHeartbeatSucceeded && IsLeaseNearOrPastExpiry(lease, nowUtc))
        {
            return EscalatedBase + EscalatedJitterMax * Math.Clamp(jitterFraction, 0, 1);
        }

        return TimeSpan.FromSeconds(normalIntervalSeconds);
    }

    private static bool IsLeaseNearOrPastExpiry(SessionLeaseDto? lease, DateTimeOffset nowUtc) =>
        lease is not null && lease.ExpiresAtUtc - nowUtc <= RefreshThreshold;
}
