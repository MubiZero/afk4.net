using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Overlay;

public enum OverlayKind
{
    /// <summary>Сообщение клуба — команда message.</summary>
    ClubMessage,

    /// <summary>До конца сессии 10 или 5 минут (кадр 07 концепта): коротко, пока впереди игра.</summary>
    TimeWarning,

    /// <summary>Последняя минута сессии, пока впереди игра (кадр 07 концепта).</summary>
    LastMinute
}

/// <param name="WarningMinutes">У <see cref="OverlayKind.TimeWarning"/> — о какой отметке речь: 10 или 5.</param>
public sealed record OverlayContent(OverlayKind Kind, string? Text = null, int? RemainingSeconds = null, int? WarningMinutes = null);

public sealed record ClubMessage(string Text, DateTimeOffset ReceivedAt);

/// <summary>
/// Что показать поверх игры. Окно нативное (спека оболочки, §7): второй WebView2 стоил бы сотню
/// мегабайт ради трёх строк.
/// </summary>
public static class OverlayPresenter
{
    /// <summary>Сколько висит сообщение клуба: прочесть две строки и не мешать дальше.</summary>
    public static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(12);

    /// <summary>За сколько минут до конца предупредить поверх игры (кадр 07).</summary>
    public static readonly IReadOnlyList<int> WarningMinutes = [10, 5];

    /// <summary>
    /// Сколько висит предупреждение: заметить краем глаза и играть дальше. Окно считается от самой
    /// отметки, поэтому ему не нужна память: остаток 9:55 — ещё предупреждение, 9:49 — уже нет.
    /// </summary>
    public static readonly TimeSpan WarningLifetime = TimeSpan.FromSeconds(10);

    public static OverlayContent? Decide(
        PlayerShellStateDto? state,
        int? remainingSecondsNow,
        bool shellInFront,
        ClubMessage? message,
        DateTimeOffset now)
    {
        if (message is not null && now - message.ReceivedAt < MessageLifetime)
        {
            return new OverlayContent(OverlayKind.ClubMessage, Text: message.Text);
        }

        // Экран оболочки впереди — время он показывает сам.
        if (shellInFront)
        {
            return null;
        }

        if (state?.State == PlayerShellStateNames.Ending)
        {
            return new OverlayContent(OverlayKind.LastMinute, RemainingSeconds: Math.Max(0, remainingSecondsNow ?? 0));
        }

        // У открытого счёта конца нет — и предупреждать не о чем. Без связи сессия тоже кончится.
        if (state?.State is PlayerShellStateNames.Active or PlayerShellStateNames.Grace && remainingSecondsNow is { } left)
        {
            foreach (var minutes in WarningMinutes)
            {
                var mark = minutes * 60;
                if (left <= mark && left > mark - WarningLifetime.TotalSeconds)
                {
                    return new OverlayContent(OverlayKind.TimeWarning, RemainingSeconds: left, WarningMinutes: minutes);
                }
            }
        }

        return null;
    }
}
