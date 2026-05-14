namespace AFK4.Agent.Service.Enforcement;

public interface IProcessLauncher
{
    Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken);
}
