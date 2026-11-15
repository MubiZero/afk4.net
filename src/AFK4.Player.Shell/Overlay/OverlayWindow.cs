using AFK4.Localization;
using AFK4.Player.Shell.Shell;
using AFK4.WebHost;

namespace AFK4.Player.Shell.Overlay;

/// <summary>
/// Окно поверх игры (спека оболочки, §7): сообщение клуба, 10 и 5 минут до конца, последняя минута. Не забирает фокус —
/// посреди боя окно, укравшее клавиатуру, хуже любого сообщения. Игры в исключительном
/// полноэкранном режиме его не покажут; это ограничение Windows, а не окна. Рисует его <see cref="NativeCard"/>: GDI,
/// без анимаций и теней — окно не должно стоить игре кадра.
/// </summary>
public sealed class OverlayWindow
{
    private readonly LocalizationService localization;
    private readonly NativeCard card = new();
    private (string Heading, string Body, string? Button)? shown;

    public OverlayWindow(LocalizationService localization)
    {
        this.localization = localization;
        card.ButtonClicked += () => ExtendRequested?.Invoke();
    }

    /// <summary>Игрок нажал «Продлить»: вывести оболочку вперёд.</summary>
    public event Action? ExtendRequested;

    public void Present(OverlayContent? content)
    {
        if (content is null)
        {
            if (shown is not null)
            {
                card.Hide();
                shown = null;
            }

            return;
        }

        string heading;
        string body;
        string? button = null;
        if (content.Kind == OverlayKind.ClubMessage)
        {
            heading = localization.T("playerShell.overlay.clubMessage");
            body = content.Text ?? string.Empty;
        }
        else
        {
            heading = localization.T(content.Kind switch
            {
                OverlayKind.TimeWarning when content.WarningMinutes == 5 => "playerShell.overlay.fiveMinutes",
                OverlayKind.TimeWarning => "playerShell.overlay.tenMinutes",
                _ => "playerShell.overlay.lastMinute"
            });
            body = RemainingTimeFormatter.Format(content.RemainingSeconds);
            // Кнопка ничего не списывает: выводит оболочку вперёд, где продление подтверждают.
            button = localization.T("playerShell.overlay.extend");
        }

        // Звать опрос можно четыре раза в секунду: перерисовывается только то, что изменилось.
        var next = (heading, body, button);
        if (shown == next)
        {
            return;
        }

        shown = next;
        card.Show(heading, body, button);
    }

    public void Close() => card.Close();
}
