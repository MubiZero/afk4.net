using System.Runtime.InteropServices;

namespace AFK4.WebHost;

/// <summary>Окно Windows без WPF: user32/gdi32/shell32 — только то, чем пользуется <see cref="WebHostWindow"/>.</summary>
internal static unsafe partial class Win32
{
    public const int WmDestroy = 0x0002;
    public const int WmSize = 0x0005;
    public const int WmSetFocus = 0x0007;
    public const int WmPaint = 0x000F;
    public const int WmClose = 0x0010;
    public const int WmEraseBackground = 0x0014;
    public const int WmMouseActivate = 0x0021;
    public const int WmLButtonDown = 0x0201;
    public const int WmTimer = 0x0113;
    public const int MaNoActivate = 3;
    public const int WmGetMinMaxInfo = 0x0024;
    public const int WmDisplayChange = 0x007E;
    public const int WmNcCalcSize = 0x0083;
    public const int WmNcHitTest = 0x0084;
    public const int WmNcLButtonDown = 0x00A1;
    public const int WmSysCommand = 0x0112;
    public const int WmApp = 0x8000;
    public const int ScClose = 0xF060;

    public const int SizeMaximized = 2;
    public const int HtClient = 1;
    public const int HtCaption = 2;
    public const int HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14, HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;

    public const uint WsOverlappedWindow = 0x00CF0000;
    public const uint WsPopup = 0x80000000;
    public const uint WsThickFrame = 0x00040000;
    public const uint WsSysMenu = 0x00080000;
    public const uint WsMinimizeBox = 0x00020000;
    public const uint WsMaximizeBox = 0x00010000;
    public const uint WsClipChildren = 0x02000000;
    public const uint WsExTopmost = 0x00000008;
    public const uint WsExToolWindow = 0x00000080;
    public const uint WsExNoActivate = 0x08000000;

    // DrawText.
    public const uint DtCenter = 0x1, DtWordBreak = 0x10, DtCalcRect = 0x400, DtNoPrefix = 0x800;
    public const int Transparent = 1;
    public const int SmCxScreen = 0;
    public const int SmXVirtualScreen = 76, SmYVirtualScreen = 77, SmCxVirtualScreen = 78, SmCyVirtualScreen = 79;
    public const uint SpiGetWorkArea = 0x0030;
    public const int DwmwaWindowCornerPreference = 33, DwmwaBorderColor = 34, DwmwcpRound = 2;

    // Панель рабочего стола (appbar).
    public const uint AbmNew = 0, AbmRemove = 1, AbmQueryPos = 2, AbmSetPos = 3, AbeTop = 1;
    public const int AbnPosChanged = 1;

    public const int SwShow = 5;
    public const int SwShowNoActivate = 4;
    public const int SwMinimize = 6;
    public const int SwMaximize = 3;
    public const int SwRestore = 9;
    public const int CwUseDefault = unchecked((int)0x80000000);
    public const int LogPixelsX = 88;
    public const uint MonitorDefaultToNearest = 2;
    public static readonly IntPtr HwndTopmost = -1;
    public static readonly IntPtr HwndNoTopmost = -2;
    public static readonly IntPtr HwndMessage = -3;
    public const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010;
    public const byte VkMenu = 0x12;
    public const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement { public int Length, Flags, ShowCmd; public Point MinPosition, MaxPosition; public Rect NormalPosition; }

    [StructLayout(LayoutKind.Sequential)]
    public struct AppBarData { public int Size; public IntPtr Hwnd; public uint CallbackMessage, Edge; public Rect Rect; public IntPtr Param; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PaintStruct
    {
        public IntPtr Dc;
        public int Erase;
        public Rect Paint;
        public int Restore, IncUpdate;
        public fixed byte Reserved[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg { public IntPtr Hwnd; public uint Message; public IntPtr WParam, LParam; public uint Time; public Point Pt; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WndClassEx
    {
        public int Size;
        public uint Style;
        public delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, IntPtr> WndProc;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        public char* MenuName, ClassName;
        public IntPtr IconSmall;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW")] public static partial ushort RegisterClassEx(WndClassEx* windowClass);
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")] public static partial IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")] public static partial int GetMessage(Msg* message, IntPtr hwnd, uint min, uint max);
    [LibraryImport("user32.dll")] public static partial int TranslateMessage(Msg* message);
    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")] public static partial IntPtr DispatchMessage(Msg* message);
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")] public static partial int PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")] public static partial IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [LibraryImport("user32.dll")] public static partial void PostQuitMessage(int exitCode);
    [LibraryImport("user32.dll")] public static partial UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint milliseconds, IntPtr callback);
    [LibraryImport("user32.dll")] public static partial int MoveWindow(IntPtr hwnd, int x, int y, int width, int height, int repaint);
    [LibraryImport("kernel32.dll")] public static partial uint GetCurrentThreadId();
    [LibraryImport("user32.dll")] public static partial int ShowWindow(IntPtr hwnd, int command);
    [LibraryImport("user32.dll")] public static partial int DestroyWindow(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial int IsZoomed(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial int IsIconic(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial int SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [LibraryImport("user32.dll")] public static partial IntPtr GetForegroundWindow();
    [LibraryImport("user32.dll")] public static partial int SetForegroundWindow(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial int BringWindowToTop(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr processId);
    [LibraryImport("user32.dll")] public static partial int AttachThreadInput(uint attach, uint attachTo, int doAttach);
    [LibraryImport("user32.dll")] public static partial void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [LibraryImport("user32.dll")] public static partial int GetClientRect(IntPtr hwnd, Rect* rect);
    [LibraryImport("user32.dll")] public static partial int GetWindowRect(IntPtr hwnd, Rect* rect);
    [LibraryImport("user32.dll")] public static partial int ReleaseCapture();
    [LibraryImport("user32.dll")] public static partial IntPtr SetFocus(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial int SetProcessDPIAware();
    [LibraryImport("user32.dll")] public static partial IntPtr GetDC(IntPtr hwnd);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [LibraryImport("user32.dll")] public static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")] public static partial int GetMonitorInfo(IntPtr monitor, MonitorInfo* info);
    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")] public static partial IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")] public static partial IntPtr LoadIcon(IntPtr instance, IntPtr name);
    [LibraryImport("gdi32.dll")] public static partial int GetDeviceCaps(IntPtr dc, int index);
    [LibraryImport("gdi32.dll")] public static partial IntPtr CreateSolidBrush(uint color);
    [LibraryImport("user32.dll")] public static partial int FillRect(IntPtr dc, Rect* rect, IntPtr brush);
    [LibraryImport("user32.dll")] public static partial int GetSystemMetrics(int index);
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")] public static partial int SystemParametersInfo(uint action, uint param, Rect* value, uint flags);
    [LibraryImport("user32.dll")] public static partial int GetWindowPlacement(IntPtr hwnd, WindowPlacement* placement);
    [LibraryImport("dwmapi.dll")] public static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, int* value, int size);
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)] public static partial uint RegisterWindowMessage(string name);
    [LibraryImport("shell32.dll")] public static partial UIntPtr SHAppBarMessage(uint message, AppBarData* data);
    [LibraryImport("user32.dll")] public static partial IntPtr BeginPaint(IntPtr hwnd, PaintStruct* paint);
    [LibraryImport("user32.dll")] public static partial int EndPaint(IntPtr hwnd, PaintStruct* paint);
    [LibraryImport("user32.dll")] public static partial int InvalidateRect(IntPtr hwnd, Rect* rect, int erase);
    [LibraryImport("user32.dll")] public static partial int SetWindowRgn(IntPtr hwnd, IntPtr region, int redraw);
    [LibraryImport("user32.dll", EntryPoint = "DrawTextW")] public static partial int DrawText(IntPtr dc, char* text, int length, Rect* rect, uint format);
    [LibraryImport("gdi32.dll")] public static partial IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);
    [LibraryImport("gdi32.dll")] public static partial int FillRgn(IntPtr dc, IntPtr region, IntPtr brush);
    [LibraryImport("gdi32.dll")] public static partial int FrameRgn(IntPtr dc, IntPtr region, IntPtr brush, int width, int height);
    [LibraryImport("gdi32.dll")] public static partial uint SetTextColor(IntPtr dc, uint color);
    [LibraryImport("gdi32.dll")] public static partial int SetBkMode(IntPtr dc, int mode);
    [LibraryImport("gdi32.dll")] public static partial IntPtr SelectObject(IntPtr dc, IntPtr handle);
    [LibraryImport("gdi32.dll")] public static partial int DeleteObject(IntPtr handle);
    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateFont(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut,
        uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")] public static partial IntPtr GetModuleHandle(IntPtr name);

    /// <summary>Цвет для GDI: 0x00BBGGRR из привычного 0xRRGGBB.</summary>
    public static uint Colorref(uint rgb) => ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);

    /// <summary>Шрифт Segoe UI (есть и на Windows 7), высота в пикселях.</summary>
    public static IntPtr SegoeUi(int pixels, int weight) =>
        CreateFont(-pixels, 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr ShellExecute(IntPtr hwnd, string operation, string file, string? parameters, string? directory, int show);

    /// <summary>Системный масштаб (96 = 100%). Процесс помечен как знающий о масштабе — размеры окна в пикселях.</summary>
    public static double SystemScale()
    {
        var dc = GetDC(IntPtr.Zero);
        try
        {
            return GetDeviceCaps(dc, LogPixelsX) / 96.0;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }
}
