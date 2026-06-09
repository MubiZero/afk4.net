namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Heartbeat cadence the server hands the agent on each beat. Configurable rather than hard-coded so
/// the steady-state load (≈ PCs / interval) can be tuned, with an adaptive shorten while commands are
/// pending so the HTTP-poll fallback drains them fast without paying that cost when idle.
/// </summary>
public sealed class HeartbeatOptions
{
    public const string ConfigurationSection = "Heartbeat";

    // Steady-state cadence; bounds load at scale (e.g. 5000 PCs @ 10s ≈ 500 req/s).
    public int DefaultIntervalSeconds { get; set; } = 10;

    // Shortened cadence while a device has commands pending, so a degraded (SignalR-down) device
    // still picks up lock/unlock within a few seconds over the poll path.
    public int ActiveIntervalSeconds { get; set; } = 3;

    // Hard floor so an override or a runaway command queue can't shorten the interval into a DoS.
    public int MinIntervalSeconds { get; set; } = 3;

    // Hard ceiling so a misconfiguration can't make a device effectively go silent.
    public int MaxIntervalSeconds { get; set; } = 60;
}
