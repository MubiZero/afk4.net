using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Overlay;

public enum OverlayKind
{
    /// <summary>Сообщение клуба — команда message.</summary>
    ClubMessage,

    /// <summary>Последняя минута сессии, пока впереди игра (кадр 07 концепта).</summary>
    LastMinute
}

public sealed record OverlayContent(OverlayKind Kind, string? Text = null, int? RemainingSeconds = null);

public sealed record ClubMessage(string Text, DateTimeOffset ReceivedAt);

/// <summary>
/// Что показать поверх игры. Окно нативное (спека оболочки, §7): второй WebView2 стоил бы сотню
/// мегабайт ради трёх строк.
/// </summary>
public static class OverlayPresenter
{
    /// <summary>Сколько висит сообщение клуба: прочесть две строки и не мешать дальше.</summary>
    public static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(12);

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

        // Экран оболочки впереди — последнюю минуту показывает он сам.
        if (!shellInFront && state?.State == PlayerShellStateNames.Ending)
        {
            return new OverlayContent(OverlayKind.LastMinute, RemainingSeconds: Math.Max(0, remainingSecondsNow ?? 0));
        }

        return null;
    }
}
