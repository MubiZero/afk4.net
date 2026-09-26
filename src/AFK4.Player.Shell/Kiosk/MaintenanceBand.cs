using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AFK4.Player.Shell.Kiosk;

/// <summary>
/// Полоса обслуживания вдоль верхнего края экрана (спека оболочки, §6.5). Окно оболочки
/// регистрируется панелью рабочего стола (appbar), как панель задач: Windows вычитает полосу из
/// рабочей области, и развёрнутые окна техника под неё не залезают, а сама полоса не закрывает
/// ему ничего.
/// </summary>
public sealed class MaintenanceBand(Window window) : IDisposable
{
    private const uint AbmNew = 0;
    private const uint AbmRemove = 1;
    private const uint AbmQueryPos = 2;
    private const uint AbmSetPos = 3;
    private const uint AbeTop = 1;
    private const int AbnPosChanged = 1;
    private const int SmCxScreen = 0;

    private HwndSource? source;
    private uint callbackMessage;
    private int heightPixels;

    public bool IsDocked => source is not null;

    /// <summary>Встать полосой сверху; повторный вызов перестраивает её (сменилось DPI или экран).</summary>
    public void Dock(double heightDip)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        heightPixels = (int)Math.Ceiling(heightDip * VisualTreeHelper.GetDpi(window).DpiScaleY);
        if (source is null)
        {
            window.WindowState = WindowState.Normal;
            callbackMessage = RegisterWindowMessage("AFK4.MaintenanceBand");
            var data = Data(handle);
            SHAppBarMessage(AbmNew, ref data);
            source = HwndSource.FromHwnd(handle);
            source?.AddHook(OnMessage);
        }

        Position(handle);
    }

    /// <summary>Снять полосу: рабочая область Windows возвращается целиком.</summary>
    public void Undock()
    {
        if (source is null)
        {
            return;
        }

        var data = Data(source.Handle);
        SHAppBarMessage(AbmRemove, ref data);
        source.RemoveHook(OnMessage);
        source = null;
    }

    public void Dispose() => Undock();

    private void Position(IntPtr handle)
    {
        var data = Data(handle);
        data.uEdge = AbeTop;
        data.rc = new Rect32 { Left = 0, Top = 0, Right = GetSystemMetrics(SmCxScreen), Bottom = heightPixels };
        // Windows может сдвинуть полосу под другую панель вдоль того же края — спрашиваем, где можно.
        SHAppBarMessage(AbmQueryPos, ref data);
        data.rc.Bottom = data.rc.Top + heightPixels;
        SHAppBarMessage(AbmSetPos, ref data);
        MoveWindow(handle, data.rc.Left, data.rc.Top, data.rc.Right - data.rc.Left, data.rc.Bottom - data.rc.Top, repaint: true);
    }

    // Панель задач переехала или сменилось разрешение — Windows просит встать заново.
    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == callbackMessage && wParam.ToInt32() == AbnPosChanged)
        {
            Position(hwnd);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private AppBarData Data(IntPtr handle) => new()
    {
        cbSize = Marshal.SizeOf<AppBarData>(),
        hWnd = handle,
        uCallbackMessage = callbackMessage
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public Rect32 rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);
}
