using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

/// <summary>
/// Игра на переднем плане (спека оболочки, §8): страница засыпает, чтобы не отнимать у игры кадр.
/// Засыпает не сразу, а через секунду: Alt+Tab туда и обратно не должен будить и усыплять
/// WebView2 на каждом нажатии. Просыпается сразу — игрок не ждёт своего экрана.
/// </summary>
public sealed class GameForegroundTracker(TimeSpan settle)
{
    public static readonly TimeSpan DefaultSettle = TimeSpan.FromSeconds(1);

    private DateTimeOffset? gameSince;

    public bool GameActive { get; private set; }

    /// <summary>Новое значение, если оно сменилось; null — всё как было.</summary>
    public bool? Observe(bool shellInFront, PlayerShellStateDto? state, DateTimeOffset now)
    {
        // Чужое окно впереди на запертом ПК — не игра: усыплять экран блокировки нельзя.
        if (shellInFront || !ShellWindowPolicy.SessionRuns(state))
        {
            gameSince = null;
            if (!GameActive)
            {
                return null;
            }

            GameActive = false;
            return false;
        }

        gameSince ??= now;
        if (GameActive || now - gameSince < settle)
        {
            return null;
        }

        GameActive = true;
        return true;
    }
}
