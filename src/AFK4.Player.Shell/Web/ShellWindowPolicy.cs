using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

/// <summary>
/// Окно оболочки «поверх всех» только на запертом экране (спека оболочки, §6.2). В сессии оно
/// обычное, иначе игра не выйдет на передний план; при блокировке хост возвращает его наверх.
/// </summary>
public static class ShellWindowPolicy
{
    /// <summary>За ПК играют: по аренде, в льготном окне или в последнюю минуту.</summary>
    public static bool SessionRuns(PlayerShellStateDto? state) =>
        state?.State is PlayerShellStateNames.Active or PlayerShellStateNames.Grace or PlayerShellStateNames.Ending;

    /// <summary>Нет состояния — значит, заперто: без слова агента ПК не открывается.</summary>
    public static bool ShouldStayOnTop(PlayerShellStateDto? state) => !SessionRuns(state);
}
