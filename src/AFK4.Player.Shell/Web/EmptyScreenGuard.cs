namespace AFK4.Player.Shell.Web;

/// <summary>
/// Экран во время сессии не бывает пустым. Игра закрылась или свёрнута, оболочка скрыта и спит, окон
/// нет — игрок видит тёмный фон, и выход знает один Alt+Tab. Страж говорит, когда оболочку пора
/// вернуть вперёд: окон приложений нет уже дольше settle.
///
/// Повторяет попытку каждые settle, пока так и есть: Windows может отказать в переднем плане, и одна
/// неудача не должна оставить игрока перед пустотой.
/// </summary>
public sealed class EmptyScreenGuard(TimeSpan settle)
{
    public static readonly TimeSpan DefaultSettle = TimeSpan.FromSeconds(2);

    private DateTimeOffset? emptySince;
    private DateTimeOffset? lastFiredAt;

    /// <returns>true — вернуть оболочку вперёд сейчас.</returns>
    public bool Observe(bool sessionRuns, bool shellInFront, bool appWindowVisible, DateTimeOffset now)
    {
        // Запертый экран — дело самой оболочки, а экран с окном приложения не пуст.
        if (!sessionRuns || shellInFront || appWindowVisible)
        {
            emptySince = null;
            lastFiredAt = null;
            return false;
        }

        emptySince ??= now;
        if (now - emptySince < settle || now - lastFiredAt < settle)
        {
            return false;
        }

        lastFiredAt = now;
        return true;
    }
}
