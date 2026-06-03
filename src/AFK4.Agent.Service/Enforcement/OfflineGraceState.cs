namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Tracks the inputs the offline grace policy needs: the agent-local time of the last successful
/// backend contact and the effective grace window the backend last advertised. The contact time is
/// stamped by the agent's own clock (spec §8) so the policy is robust to absolute-clock drift on the
/// gaming PC. Before the first successful heartbeat the window falls back to the global default.
/// </summary>
public interface IOfflineGraceState
{
    DateTimeOffset? LastSuccessfulContactUtc { get; }

    int EffectiveGraceMinutes { get; }

    void RecordSuccessfulContact(DateTimeOffset contactAtUtc, int effectiveGraceMinutes);
}

public sealed class OfflineGraceState : IOfflineGraceState
{
    private const int DefaultGraceMinutes = 15;
    private readonly object syncRoot = new();
    private DateTimeOffset? lastSuccessfulContactUtc;
    private int effectiveGraceMinutes = DefaultGraceMinutes;

    public DateTimeOffset? LastSuccessfulContactUtc
    {
        get { lock (syncRoot) { return lastSuccessfulContactUtc; } }
    }

    public int EffectiveGraceMinutes
    {
        get { lock (syncRoot) { return effectiveGraceMinutes; } }
    }

    public void RecordSuccessfulContact(DateTimeOffset contactAtUtc, int effectiveGraceMinutes)
    {
        lock (syncRoot)
        {
            lastSuccessfulContactUtc = contactAtUtc;
            this.effectiveGraceMinutes = effectiveGraceMinutes;
        }
    }
}
