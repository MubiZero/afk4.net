using AFK4.Agent.Service.Enforcement;

namespace AFK4.Agent.Service.Shell;

public interface IPlayerShellProcessSupervisor
{
    Task EnsureRunningAsync(AgentRuntimeState runtimeState, CancellationToken cancellationToken);
}
