using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

/// <summary>Как окно оболочки стоит на экране.</summary>
public enum ShellWindowLayout
{
    /// <summary>Во весь экран поверх всех: запертый ПК, мимо не пройти.</summary>
    Cover,

    /// <summary>Во весь экран, но обычным окном: идёт сессия, и игра должна выходить вперёд.</summary>
    Behind,

    /// <summary>Полоса сверху: обслуживание, под ней рабочий стол техника.</summary>
    Band
}

/// <summary>
/// Окно оболочки «поверх всех» только на запертом экране (спека оболочки, §6.2). В сессии оно
/// обычное, иначе игра не выйдет на передний план; при блокировке хост возвращает его наверх.
/// В обслуживании (§6.5) окно сжимается в полосу «ПК на обслуживании · Вернуть в зал».
/// </summary>
public static class ShellWindowPolicy
{
    /// <summary>Высота полосы обслуживания, в независимых от DPI единицах.</summary>
    public const double BandHeight = 56;

    /// <summary>За ПК играют: по аренде, в льготном окне или в последнюю минуту.</summary>
    public static bool SessionRuns(PlayerShellStateDto? state) =>
        state?.State is PlayerShellStateNames.Active or PlayerShellStateNames.Grace or PlayerShellStateNames.Ending;

    /// <summary>Нет состояния — значит, заперто: без слова агента ПК не открывается.</summary>
    public static ShellWindowLayout Layout(PlayerShellStateDto? state) =>
        state?.State == PlayerShellStateNames.Maintenance ? ShellWindowLayout.Band
        : SessionRuns(state) ? ShellWindowLayout.Behind
        : ShellWindowLayout.Cover;

    public static bool ShouldStayOnTop(PlayerShellStateDto? state) => Layout(state) == ShellWindowLayout.Cover;
}
