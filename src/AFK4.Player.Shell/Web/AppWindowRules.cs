namespace AFK4.Player.Shell.Web;

/// <summary>Окно верхнего уровня глазами Windows — столько, сколько нужно, чтобы решить, видит ли его игрок.</summary>
public readonly record struct WindowInfo(
    IntPtr Handle,
    uint ProcessId,
    bool Visible,
    bool Minimized,
    bool Owned,
    bool ToolWindow,
    bool Cloaked,
    int Width,
    int Height);

/// <summary>
/// Что считается окном приложения. Окно игры, браузера, лаунчера — да; скрытые, свёрнутые, служебные
/// (подсказки, окна-владельцы, крошечные вспомогательные) и окна самой оболочки — нет. Решение чистое:
/// Windows спрашивается в <c>NativeWindows</c>, а здесь — только что из ответа считать экраном.
/// </summary>
public static class AppWindowRules
{
    /// <summary>Меньше — это вспомогательное окно: у игр и программ такие бывают без единого пикселя на экране.</summary>
    private const int MinWidth = 200;
    private const int MinHeight = 120;

    /// <summary>Окно, которое игрок сейчас видит на экране.</summary>
    public static bool IsAppWindow(WindowInfo window, uint shellProcessId) =>
        IsCandidate(window, shellProcessId) && !window.Minimized && window.Width >= MinWidth && window.Height >= MinHeight;

    /// <summary>Окна заданных процессов, свёрнутые тоже: «Вернуться» разворачивает их.</summary>
    public static IReadOnlyList<WindowInfo> OfProcesses(
        IEnumerable<WindowInfo> windows,
        IReadOnlyCollection<int> processIds,
        uint shellProcessId) =>
        windows
            .Where(window => IsCandidate(window, shellProcessId)
                && processIds.Contains((int)window.ProcessId)
                && (window.Minimized || (window.Width >= MinWidth && window.Height >= MinHeight)))
            .ToList();

    private static bool IsCandidate(WindowInfo window, uint shellProcessId) =>
        window.Visible && !window.Owned && !window.ToolWindow && !window.Cloaked && window.ProcessId != shellProcessId;
}
