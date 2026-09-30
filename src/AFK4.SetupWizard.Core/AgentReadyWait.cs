namespace AFK4.SetupWizard.Core;

/// <summary>Служба запущена, а агент настройку не принял: на связь такой ПК не выйдет.</summary>
public sealed class AgentDidNotAcceptSettingsException(string message) : Exception(message);

public static class AgentReadyWait
{
    /// <summary>
    /// Ждёт, пока агент положит отметку о принятой настройке (<c>AgentReadyMarker</c>).
    /// Опрос файла, а не канал: агент работает под SYSTEM, мастер — отдельный процесс, и общий
    /// каталог состояния у них уже есть.
    /// </summary>
    public static void ForMarker(string markerPath, TimeSpan timeout, TimeSpan pollInterval)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!File.Exists(markerPath))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new AgentDidNotAcceptSettingsException(
                    $"The agent service is running but did not confirm its settings within {timeout.TotalSeconds:0} s. "
                    + "An agent with unusable settings idles: check its log.");
            }

            Thread.Sleep(pollInterval);
        }
    }
}
