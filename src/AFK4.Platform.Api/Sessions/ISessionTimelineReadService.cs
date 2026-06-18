using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Reads started sessions (running and completed) that overlap a time window, for the booking
/// timeline. Distinct from the floor-map read service, which exposes only the current snapshot.
/// </summary>
public interface ISessionTimelineReadService
{
    Task<SessionTimelineResult> GetSessionsAsync(
        Guid organizationId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
