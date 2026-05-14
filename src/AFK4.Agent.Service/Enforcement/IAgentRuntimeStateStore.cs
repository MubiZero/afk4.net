using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Enforcement;

public interface IAgentRuntimeStateStore
{
    AgentRuntimeState Current { get; }

    void Save(AgentRuntimeState state);

    void MarkLocked(DateTimeOffset observedAtUtc);

    void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc);
}
