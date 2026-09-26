using AFK4.Agent.Service.Enforcement;

namespace AFK4.Agent.Service.Games;

public interface ISessionAutostart
{
    /// <summary>Сессия началась: запустить то, что клуб отметил «запускать в начале сессии».</summary>
    Task StartAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Автозапуск в начале сессии (настройки ПК клуба): Discord, клиент Steam. Не запустилось одно —
/// остальное всё равно стартует, а сессия не страдает: это удобство, не условие игры.
/// </summary>
public sealed class SessionAutostart(ILauncherCatalog catalog, IProcessLauncher launcher, ILogger<SessionAutostart> logger) : ISessionAutostart
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in catalog.Entries().Where(entry => entry.LaunchOnSessionStart))
        {
            if (entry.ExecutablePath is null || !File.Exists(entry.ExecutablePath))
            {
                logger.LogInformation("Autostart skipped {AppId}: it is not installed on this PC.", entry.AppId);
                continue;
            }

            try
            {
                await launcher.LaunchAsync(entry.ExecutablePath, entry.Arguments, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Autostart of {AppId} failed.", entry.AppId);
            }
        }
    }
}
