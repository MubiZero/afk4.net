using System.Runtime.InteropServices;

namespace AFK4.Player.Shell.Web;

/// <summary>Что Windows знает о вводе и переднем окне. Тонкая обёртка: решения — в чистых классах рядом.</summary>
internal static class NativeInput
{
    public static uint LastInputTick()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? info.Time : NowTick();
    }

    public static uint NowTick() => unchecked((uint)Environment.TickCount);

    /// <summary>Впереди окно этого процесса — экран оболочки или окно поверх игры.</summary>
    public static bool ShellInFront()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
