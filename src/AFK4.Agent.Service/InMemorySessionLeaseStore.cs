using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service;

public sealed class InMemorySessionLeaseStore : ISessionLeaseStore
{
    public SessionLeaseDto? Current { get; private set; }

    public void Save(SessionLeaseDto lease)
    {
        Current = lease;
    }

    public void Clear(Guid? sessionId)
    {
        if (sessionId is null || Current?.SessionId == sessionId)
        {
            Current = null;
        }
    }
}
