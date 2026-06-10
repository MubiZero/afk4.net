namespace AFK4.Agent.Service;

/// <summary>
/// Compares the agent's local clock against the server's clock (carried in every heartbeat response as
/// ServerTimeUtc) and flags when they diverge beyond <see cref="WarningThreshold"/>. Clock skew between a
/// gaming PC and the backend is the main latent risk in lease/grace timing — the agent measures the
/// offline grace window on its own clock — so this surfaces drift in the logs instead of leaving us blind.
/// Pure: the caller supplies both clock reads. The measured drift includes one-way network latency
/// (~RTT/2), which is negligible against a multi-second threshold.
/// </summary>
public static class ClockDriftCheck
{
    public static readonly TimeSpan WarningThreshold = TimeSpan.FromSeconds(30);

    // Positive => the agent clock is behind the server; negative => the agent clock is ahead.
    public static TimeSpan Drift(DateTimeOffset serverTimeUtc, DateTimeOffset agentNowUtc) =>
        serverTimeUtc - agentNowUtc;

    public static bool IsExcessive(TimeSpan drift, TimeSpan threshold) =>
        drift.Duration() >= threshold;
}
