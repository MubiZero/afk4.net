using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AFK4.Player.Shell.Kiosk;

/// <summary>
/// Низкоуровневый перехват клавиатуры (<c>WH_KEYBOARD_LL</c>) в своём потоке с очередью сообщений:
/// поток окна WPF занят рисованием, и медленный кадр не должен снимать перехват. Решение — в
/// <see cref="KeyboardBlockPolicy"/>; здесь только чтение модификаторов и ответ Windows.
/// </summary>
public sealed class KioskKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmQuit = 0x0012;
    private const int LlkhfAltDown = 0x20;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;

    private readonly LowLevelKeyboardProc callback;
    private readonly Thread thread;
    private volatile ShellKeyMode mode = ShellKeyMode.Locked;
    private volatile bool shellInFront = true;
    private uint threadId;
    private IntPtr hook;

    public KioskKeyboardHook()
    {
        // Делегат живёт в поле: иначе сборщик мусора заберёт его, пока Windows ещё зовёт перехват.
        callback = OnKey;
        thread = new Thread(Run) { IsBackground = true, Name = "AFK4 kiosk keyboard hook" };
    }

    public void Start() => thread.Start();

    /// <summary>Состояние ПК поменялось — меняется и таблица.</summary>
    public void SetMode(ShellKeyMode value) => mode = value;

    /// <summary>Что впереди, приходит от опроса окна: сам перехват переднее окно не спрашивает.</summary>
    public void SetShellInFront(bool value) => shellInFront = value;

    private void Run()
    {
        threadId = GetCurrentThreadId();
        hook = SetWindowsHookEx(WhKeyboardLl, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
        {
            PlayerShellStartupLog.Write("Keyboard hook could not be installed.", new Win32Exception(Marshal.GetLastWin32Error()));
            return;
        }

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        UnhookWindowsHookEx(hook);
    }

    private IntPtr OnKey(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<KeyboardInfo>(data);
            var stroke = new KeyStroke(
                (int)info.VirtualKey,
                Alt: (info.Flags & LlkhfAltDown) != 0,
                Ctrl: (GetAsyncKeyState(VkControl) & 0x8000) != 0,
                Shift: (GetAsyncKeyState(VkShift) & 0x8000) != 0);
            // Глотается и нажатие, и отпускание: меню «Пуск» Windows открывает на отпускании Win.
            if (KeyboardBlockPolicy.ShouldBlock(mode, stroke, shellInFront))
            {
                return 1;
            }
        }

        return CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (threadId != 0)
        {
            PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInfo
    {
        public uint VirtualKey;
        public uint ScanCode;
        public int Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
