using System.Runtime.InteropServices;
using System.Text;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Player.Shell.Kiosk;

/// <summary>
/// Закрывает окна по правилам профиля защиты (спека оболочки, §6.3). <c>SetWinEventHook</c> в своём
/// потоке с очередью сообщений: служба в сессии 0 окон игрока не видит, хост — видит. Окно
/// закрывается вежливо, WM_CLOSE: то же, что крестик, — программа не теряет данные молча.
/// </summary>
public sealed class WindowBlocker : IDisposable
{
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectNameChange = 0x800C;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const int ObjidWindow = 0;
    private const uint GaRoot = 2;
    private const uint WmClose = 0x0010;
    private const int WmQuit = 0x0012;

    private readonly WinEventProc callback;
    private readonly Thread thread;
    private readonly uint ownProcessId = (uint)Environment.ProcessId;
    private volatile IReadOnlyList<BlockedWindowRuleDto> rules = [];
    private uint threadId;

    public WindowBlocker()
    {
        // Делегат в поле: иначе сборщик мусора заберёт его, пока Windows ещё зовёт перехват.
        callback = OnWinEvent;
        thread = new Thread(Run) { IsBackground = true, Name = "AFK4 window blocker" };
    }

    public void Start() => thread.Start();

    public void SetRules(IReadOnlyList<BlockedWindowRuleDto>? value) => rules = value ?? [];

    private void Run()
    {
        threadId = GetCurrentThreadId();
        // Появление окна и смена заголовка: браузер меняет заголовок, уже будучи на экране.
        var show = SetWinEventHook(EventObjectShow, EventObjectShow, IntPtr.Zero, callback, 0, 0, WineventOutOfContext | WineventSkipOwnProcess);
        var rename = SetWinEventHook(EventObjectNameChange, EventObjectNameChange, IntPtr.Zero, callback, 0, 0, WineventOutOfContext | WineventSkipOwnProcess);
        if (show == IntPtr.Zero || rename == IntPtr.Zero)
        {
            PlayerShellStartupLog.Write("Window blocker hooks could not be installed.");
        }

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        UnhookWinEvent(show);
        UnhookWinEvent(rename);
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        var current = rules;
        if (current.Count == 0 || objectId != ObjidWindow || window == IntPtr.Zero || GetAncestor(window, GaRoot) != window)
        {
            return;
        }

        GetWindowThreadProcessId(window, out var processId);
        if (!WindowRules.ShouldClose(current, TitleOf(window), ClassOf(window), processId == ownProcessId))
        {
            return;
        }

        PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    private static string TitleOf(IntPtr window)
    {
        var length = GetWindowTextLength(window);
        if (length <= 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder(length + 1);
        GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    private static string ClassOf(IntPtr window)
    {
        var text = new StringBuilder(256);
        return GetClassName(window, text, text.Capacity) > 0 ? text.ToString() : string.Empty;
    }

    public void Dispose()
    {
        if (threadId != 0)
        {
            PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Message message, IntPtr window, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
