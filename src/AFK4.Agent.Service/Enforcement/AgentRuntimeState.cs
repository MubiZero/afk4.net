using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

public sealed record AgentRuntimeState(
    string State,
    bool IsLocked,
    Guid? ActiveSessionId,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static AgentRuntimeState Locked(DateTimeOffset updatedAtUtc)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Locked,
            IsLocked: true,
            ActiveSessionId: null,
            LeaseExpiresAtUtc: null,
            updatedAtUtc);
    }

    public static AgentRuntimeState Active(SessionLeaseDto lease, DateTimeOffset updatedAtUtc)
    {
        return new AgentRuntimeState(
            PlayerShellStateNames.Active,
            IsLocked: false,
            lease.SessionId,
            lease.ExpiresAtUtc,
            updatedAtUtc);
    }
}
