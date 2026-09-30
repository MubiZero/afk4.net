namespace AFK4.Agent.Service.Enforcement;

public interface IProcessLauncher
{
    /// <summary>Запускает приложение; результат — номер процесса, null — не известен.</summary>
    Task<int?> LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken);
}
