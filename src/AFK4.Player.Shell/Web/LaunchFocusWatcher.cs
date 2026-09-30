namespace AFK4.Player.Shell.Web;

/// <summary>
/// После «Играть» оболочка должна отступить, а окно игры — выйти вперёд. Агент запускает игру в
/// сессии игрока, но право выйти на передний план у неё нет: окно оставалось под оболочкой, и игрок
/// нажимал и не видел ничего.
///
/// Наблюдатель ждёт после запуска новых окон и отдаёт каждое новое по одному разу. Он не привязан к
/// процессам: у Steam, Epic и лаунчеров окно игры открывает не тот процесс, что запустил агент, и
/// честнее «появилось новое окно», чем угадывать родство. Окно, которое игрок сам увёл назад, второй
/// раз силой не поднимается.
/// </summary>
public sealed class LaunchFocusWatcher(TimeSpan watchFor)
{
    /// <summary>Игра из Steam открывает окно спустя десятки секунд; дольше ждать — тянуть окна, о которых игрок не просил.</summary>
    public static readonly TimeSpan DefaultWatch = TimeSpan.FromSeconds(45);

    private readonly HashSet<IntPtr> seen = [];
    private DateTimeOffset? until;

    /// <param name="alreadyOpen">Окна, что были на экране до запуска: они не новые.</param>
    public void Begin(DateTimeOffset now, IReadOnlyCollection<IntPtr> alreadyOpen)
    {
        seen.Clear();
        seen.UnionWith(alreadyOpen);
        until = now + watchFor;
    }

    /// <summary>Окно, которое надо вывести вперёд; null — такого нет.</summary>
    public IntPtr? Observe(DateTimeOffset now, IReadOnlyCollection<IntPtr> open)
    {
        if (until is not { } end || now > end)
        {
            return null;
        }

        foreach (var window in open)
        {
            if (seen.Add(window))
            {
                return window;
            }
        }

        return null;
    }
}
