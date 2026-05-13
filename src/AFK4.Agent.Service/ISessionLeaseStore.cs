using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service;

public interface ISessionLeaseStore
{
    SessionLeaseDto? Current { get; }

    void Save(SessionLeaseDto lease);

    void Clear(Guid? sessionId);
}
