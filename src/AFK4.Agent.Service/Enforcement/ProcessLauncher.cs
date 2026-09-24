using AFK4.Agent.Service.Shell;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Запускает игру там, где сидит игрок.
///
/// Раньше здесь был обычный <c>Process.Start</c> из службы: служба живёт в сессии 0, и игра
/// запускалась там же — без окна, под LocalSystem. На экране не происходило ничего, а процесс с
/// правами системы продолжал висеть. Теперь игра стартует в консольной сессии тем же путём, что
/// и сама оболочка: токен пользователя этой сессии, его окружение, его рабочий стол.
/// </summary>
public sealed class ProcessLauncher(
    IPlayerShellLaunchContext launchContext,
    IPlayerShellProcessStarter processStarter) : IProcessLauncher
{
    public Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken)
    {
        var target = launchContext.GetActiveUserSession()
            ?? throw new InvalidOperationException("No interactive user session is available to launch the app into.");

        processStarter.Start(executablePath, arguments, target);
        return Task.CompletedTask;
    }
}
