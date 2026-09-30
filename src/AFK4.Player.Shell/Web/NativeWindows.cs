using System.Runtime.InteropServices;

namespace AFK4.Player.Shell.Web;

/// <summary>Вывести окна процессов вперёд — «Вернуться» в «Моих приложениях».</summary>
public interface IAppWindowControl
{
    /// <summary>true — окно нашлось и выведено; false — у этих процессов окна нет.</summary>
    bool Focus(IReadOnlyCollection<int> processIds);
}

/// <summary>Что Windows знает об окнах рабочего стола игрока. Тонкая обёртка: решения — в <see cref="AppWindowRules"/>.</summary>
internal sealed class NativeWindows : IAppWindowControl
{
    private const int GwOwner = 4;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x80;
    private const int WsExAppWindow = 0x40000;
    private const int DwmaCloaked = 14;
    private const int SwRestore = 9;
    private const byte VkMenu = 0x12;
    private const uint KeyEventKeyUp = 0x2;

    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    public static uint ShellProcessId => OwnProcessId;

    public static IReadOnlyList<WindowInfo> TopLevel()
    {
        var windows = new List<WindowInfo>();
        EnumWindows((handle, _) =>
        {
            windows.Add(Describe(handle));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    /// <summary>Окна приложений, которые игрок видит сейчас.</summary>
    public static IReadOnlyList<WindowInfo> VisibleAppWindows() =>
        TopLevel().Where(window => AppWindowRules.IsAppWindow(window, OwnProcessId)).ToList();

    public bool Focus(IReadOnlyCollection<int> processIds)
    {
        var windows = AppWindowRules.OfProcesses(TopLevel(), processIds, OwnProcessId);
        // Видимое окно — раньше свёрнутого: у игры с лаунчером их бывает несколько.
        var target = windows.FirstOrDefault(window => !window.Minimized);
        if (target.Handle == IntPtr.Zero)
        {
            target = windows.FirstOrDefault();
        }

        return target.Handle != IntPtr.Zero && BringToFront(target.Handle);
    }

    /// <summary>
    /// Вперёд, несмотря на запрет Windows отнимать передний план у чужого процесса: поток оболочки
    /// на миг подключается ко вводу того, кто впереди, — и тогда запрет не действует. Не хватило —
    /// нажатие Alt, после него Windows разрешает переключиться.
    /// </summary>
    public static bool BringToFront(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SwRestore);
        }

        var front = GetForegroundWindow();
        var frontThread = front == IntPtr.Zero ? 0 : GetWindowThreadProcessId(front, out _);
        var ownThread = GetCurrentThreadId();
        var attached = frontThread != 0 && frontThread != ownThread && AttachThreadInput(ownThread, frontThread, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(ownThread, frontThread, false);
            }
        }

        if (GetForegroundWindow() != window)
        {
            keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
            keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);
            SetForegroundWindow(window);
        }

        return GetForegroundWindow() == window;
    }

    private static WindowInfo Describe(IntPtr handle)
    {
        GetWindowThreadProcessId(handle, out var processId);
        var style = GetWindowLong(handle, GwlExStyle);
        GetWindowRect(handle, out var rect);
        var cloaked = DwmGetWindowAttribute(handle, DwmaCloaked, out var cloakedValue, sizeof(int)) == 0 && cloakedValue != 0;
        return new WindowInfo(
            handle,
            processId,
            IsWindowVisible(handle),
            IsIconic(handle),
            GetWindow(handle, GwOwner) != IntPtr.Zero,
            // Окно с отметкой «окно приложения» остаётся в списке, даже если стиль его инструментальный.
            (style & WsExToolWindow) != 0 && (style & WsExAppWindow) == 0,
            cloaked,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
