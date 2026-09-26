using System.Diagnostics;
using AFK4.Agent.Service.Shell;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Рабочий стол Windows для техника (спека оболочки, §6.5): проводник в сессии игрока на время
/// обслуживания.
/// </summary>
public interface IMaintenanceDesktop
{
    /// <summary>
    /// Открыть рабочий стол. true — проводник запустил агент, и при возврате в зал его надо закрыть;
    /// false — проводник уже работал (ПК ещё не переведён в киоск) или сессии игрока нет.
    /// </summary>
    bool Open();

    /// <summary>Закрыть проводник в сессии игрока.</summary>
    void Close();
}

public sealed class MaintenanceDesktop(
    IPlayerShellLaunchContext launchContext,
    IPlayerShellProcessStarter processStarter,
    ILogger<MaintenanceDesktop> logger) : IMaintenanceDesktop
{
    private const string ExplorerProcessName = "explorer";

    public bool Open()
    {
        var target = launchContext.GetActiveUserSession();
        if (target is null)
        {
            logger.LogWarning("No interactive session to open the maintenance desktop in.");
            return false;
        }

        var running = ExplorersIn(target.SessionId);
        running.ForEach(process => process.Dispose());
        if (running.Count > 0)
        {
            // До перевода в киоск проводник — обычная оболочка этой учётки. Закрывать его при
            // возврате в зал значило бы оставить ПК без рабочего стола.
            logger.LogInformation("Explorer already runs in session {SessionId}; leaving it as it is.", target.SessionId);
            return false;
        }

        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        processStarter.Start(explorer, string.Empty, target);
        logger.LogInformation("Maintenance desktop opened in session {SessionId}.", target.SessionId);
        return true;
    }

    public void Close()
    {
        var target = launchContext.GetActiveUserSession();
        if (target is null)
        {
            return;
        }

        foreach (var process in ExplorersIn(target.SessionId))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Процесс уже вышел или закрывается сам — цель достигнута.
                    logger.LogDebug(exception, "Explorer {ProcessId} was already on its way out.", process.Id);
                }
            }
        }

        logger.LogInformation("Maintenance desktop closed in session {SessionId}.", target.SessionId);
    }

    // Только сессия игрока: проводник техника, зашедшего по удалённому рабочему столу, не наш.
    private static List<Process> ExplorersIn(int sessionId)
    {
        var mine = new List<Process>();
        foreach (var process in Process.GetProcessesByName(ExplorerProcessName))
        {
            if (process.SessionId == sessionId)
            {
                mine.Add(process);
            }
            else
            {
                process.Dispose();
            }
        }

        return mine;
    }
}
