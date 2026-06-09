namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Resolves the heartbeat interval (seconds) the server returns to a device: the steady-state default,
/// adaptively shortened to the active cadence while the device has commands pending, hard-clamped to
/// <c>[Min, Max]</c> at the read boundary so neither a config slip nor a command backlog can push it
/// out of a safe range.
/// </summary>
public static class HeartbeatIntervalPolicy
{
    public static int Resolve(bool hasPendingCommands, HeartbeatOptions options)
    {
        var target = hasPendingCommands ? options.ActiveIntervalSeconds : options.DefaultIntervalSeconds;
        var max = Math.Max(options.MinIntervalSeconds, options.MaxIntervalSeconds);
        return Math.Clamp(target, options.MinIntervalSeconds, max);
    }
}
