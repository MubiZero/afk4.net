using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Enforcement;

public interface ISessionEnforcementCoordinator
{
    Task<SessionEnforcementResult> UnlockAsync(SessionLeaseDto lease, CancellationToken cancellationToken);

    Task<SessionEnforcementResult> RefreshLeaseAsync(SessionLeaseDto lease, CancellationToken cancellationToken);

    Task<SessionEnforcementResult> LockAsync(Guid? sessionId, CancellationToken cancellationToken);
}
