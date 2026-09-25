namespace AFK4.Agent.Service.Cleanup;

/// <summary>Процесс игрока в консольной сессии, как его видит агент.</summary>
/// <param name="ExecutablePath">Полный путь к exe; null — прочитать не удалось.</param>
/// <param name="StartedAtUtc">Когда процесс запущен; null — не удалось узнать.</param>
public sealed record SessionProcess(int ProcessId, string ImageName, string? ExecutablePath, DateTimeOffset? StartedAtUtc);

/// <summary>
/// Какие программы закрыть после сессии (спека оболочки, §6.4). Закрывается запущенное за сессию —
/// игры, лаунчеры, браузеры — и программы из каталога стирания, когда бы они ни стартовали. Всё,
/// что работало до сессии, остаётся: утилиты мыши, подсветки и звука стартуют при входе в Windows,
/// и закрытый драйвер-трей не вернулся бы до перезагрузки.
/// </summary>
public static class SessionProcessPolicy
{
    public static bool ShouldClose(
        SessionProcess process,
        DateTimeOffset? sessionStartedAtUtc,
        IReadOnlySet<string> alwaysClose,
        IReadOnlyList<string> protectedRoots)
    {
        // Чей exe не прочитался — того не трогаем: закрыть наугад хуже, чем оставить.
        if (string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            return false;
        }

        if (protectedRoots.Any(root => SessionTraceCatalog.IsInside(process.ExecutablePath, root)))
        {
            return false;
        }

        if (alwaysClose.Contains(process.ImageName))
        {
            return true;
        }

        return sessionStartedAtUtc is { } started && process.StartedAtUtc is { } processStarted && processStarted >= started;
    }
}
