namespace AFK4.Platform.Api.Platform.Pulse;

public sealed class PlatformPulseOptions
{
    public const string SectionName = "PlatformPulse";

    // A club is "silent" once none of its devices have heartbeated within this window.
    public int AgentSilenceThresholdMinutes { get; set; } = 15;

    // A shift left open longer than this is flagged as possibly stuck / forgotten.
    public int ShiftStaleHours { get; set; } = 24;
}
