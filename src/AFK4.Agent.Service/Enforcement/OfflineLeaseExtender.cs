using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Decides whether a paying customer's expired-but-signed lease may keep the PC unlocked through a
/// network outage (spec §6.1, D5). It NEVER mints a lease — it only honours an already-signed lease
/// for an <em>active</em> session, and only while the agent has been offline for less than the
/// effective grace window measured from the last successful backend contact. Past the window the
/// caller locks exactly as before.
/// </summary>
public interface IOfflineLeaseExtender
{
    bool ShouldExtend(SessionLeaseDto lease, DateTimeOffset nowUtc);
}

public sealed class OfflineLeaseExtender(IOfflineGraceState graceState) : IOfflineLeaseExtender
{
    public bool ShouldExtend(SessionLeaseDto lease, DateTimeOffset nowUtc)
    {
        if (lease is null || !string.Equals(lease.State, SessionStateNames.Active, StringComparison.Ordinal))
        {
            return false;
        }

        var lastContact = graceState.LastSuccessfulContactUtc;
        if (lastContact is null)
        {
            return false;
        }

        var graceWindow = TimeSpan.FromMinutes(graceState.EffectiveGraceMinutes);
        return nowUtc - lastContact.Value < graceWindow;
    }
}
