namespace AFK4.Agent.Service.Enforcement;

public interface IProcessPolicyEnforcer
{
    AgentLauncherAppOptions? FindAllowedLauncherApp(string appId);

    Task EnforceAsync(CancellationToken cancellationToken);
}
