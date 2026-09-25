using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Kiosk;

public enum ShellKeyMode
{
    /// <summary>Заперт: простой, окно входа, выбор времени, итог.</summary>
    Locked,

    /// <summary>Идёт сессия: игра должна жить, оболочка — не закрываться.</summary>
    Session,

    /// <summary>Обслуживание: техник работает с Windows, ничего не перехватывается.</summary>
    Maintenance
}

/// <summary>Нажатие глазами перехвата: клавиша и какие модификаторы зажаты.</summary>
public readonly record struct KeyStroke(int VirtualKey, bool Alt, bool Ctrl, bool Shift);

/// <summary>
/// Что перехват клавиш глотает (спека оболочки, §6.2). Таблица считается заранее: Windows снимает
/// перехват, если обработчик тормозит дольше LowLevelHooksTimeout, поэтому в нём только поиск по
/// готовым правилам — ни канала, ни диска.
///
/// | заперт    | Win, Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4, Ctrl+Shift+Esc |
/// | сессия    | Win, Ctrl+Esc; Alt+F4 — только для окна оболочки         |
/// | обслуживание | ничего                                                |
///
/// Ctrl+Alt+Del перехватить нельзя — его меню режут политики (§6.2, профили защиты).
/// </summary>
public static class KeyboardBlockPolicy
{
    public const int VkTab = 0x09;
    public const int VkEscape = 0x1B;
    public const int VkLeftWin = 0x5B;
    public const int VkRightWin = 0x5C;
    public const int VkF4 = 0x73;

    public static ShellKeyMode ModeFor(PlayerShellStateDto? state) =>
        state?.State == PlayerShellStateNames.Maintenance ? ShellKeyMode.Maintenance
        : ShellWindowPolicy.SessionRuns(state) ? ShellKeyMode.Session
        : ShellKeyMode.Locked;

    /// <param name="shellInFront">Впереди окно оболочки: в сессии Alt+F4 закрывает игру, но не оболочку.</param>
    public static bool ShouldBlock(ShellKeyMode mode, KeyStroke key, bool shellInFront)
    {
        if (mode == ShellKeyMode.Maintenance)
        {
            return false;
        }

        var win = key.VirtualKey is VkLeftWin or VkRightWin;
        var ctrlEsc = key.VirtualKey == VkEscape && key.Ctrl && !key.Shift && !key.Alt;
        if (win || ctrlEsc)
        {
            return true;
        }

        var altF4 = key.VirtualKey == VkF4 && key.Alt;
        if (mode == ShellKeyMode.Session)
        {
            return altF4 && shellInFront;
        }

        var altTab = key.VirtualKey == VkTab && key.Alt;
        var altEsc = key.VirtualKey == VkEscape && key.Alt;
        var taskManager = key.VirtualKey == VkEscape && key.Ctrl && key.Shift;
        return altTab || altEsc || altF4 || taskManager;
    }
}
