namespace AFK4.Platform.Api.Sessions;

/// <summary>Tuning for <see cref="AutoProtectionRunner"/> / the hosted ticker.</summary>
public sealed class AutoProtectionOptions
{
    /// <summary>How often the background service evaluates active sessions.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long before a fixed session's end the warning overlay is pushed.</summary>
    public TimeSpan WarnBeforeExpiry { get; set; } = TimeSpan.FromMinutes(5);
}
