using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

public interface IPlatformAlertNotifier
{
    Task NotifyOpenedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken);

    Task NotifyResolvedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken);
}
