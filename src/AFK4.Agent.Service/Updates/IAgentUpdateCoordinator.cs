namespace AFK4.Agent.Service.Updates;

public interface IAgentUpdateCoordinator
{
    Task<AgentUpdateExecutionResult> CheckAndApplyUpdatesAsync(CancellationToken cancellationToken);
}
