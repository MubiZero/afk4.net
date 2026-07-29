using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Updates;

public sealed class OrganizationAdminActivityState
{
    private int criticalCommandCount;

    public bool HasCriticalCommandInFlight => Volatile.Read(ref criticalCommandCount) > 0;

    public IDisposable BeginCriticalCommand()
    {
        Interlocked.Increment(ref criticalCommandCount);
        return new CriticalCommandLease(this);
    }

    public string QueryStatus() => HasCriticalCommandInFlight
        ? LocalUpdateCoordinationStatuses.CriticalCommandActive
        : LocalUpdateCoordinationStatuses.Idle;

    private sealed class CriticalCommandLease(OrganizationAdminActivityState owner) : IDisposable
    {
        private OrganizationAdminActivityState? currentOwner = owner;

        public void Dispose()
        {
            var target = Interlocked.Exchange(ref currentOwner, null);
            if (target is not null) Interlocked.Decrement(ref target.criticalCommandCount);
        }
    }
}
