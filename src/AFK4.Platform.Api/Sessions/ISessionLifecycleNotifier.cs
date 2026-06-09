using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Publishes a session-lifecycle change to operator clients. A thin seam over the DeviceHub so the
/// session services don't take a direct hub dependency and stay unit-testable with a recording fake.
/// Callers broadcast <b>after</b> the mutation commits, so a rolled-back command never emits.
/// </summary>
public interface ISessionLifecycleNotifier
{
    Task NotifyAsync(SessionLifecycleChangedDto change, CancellationToken cancellationToken);
}

public sealed class SignalRSessionLifecycleNotifier(IHubContext<DeviceHub> hubContext) : ISessionLifecycleNotifier
{
    public Task NotifyAsync(SessionLifecycleChangedDto change, CancellationToken cancellationToken)
    {
        return hubContext.Clients
            .Group(DeviceHubGroups.Branch(change.BranchId))
            .SendAsync(DeviceRealtimeEvents.SessionLifecycleChanged, change, cancellationToken);
    }
}
