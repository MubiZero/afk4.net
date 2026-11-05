using System.Runtime.InteropServices;

namespace AFK4.WebHost;

/// <summary>
/// Вызовы Miniblink (mb.h из выпуска miniblink132). На Windows x86 соглашение — stdcall, на x64 оно одно.
/// Все вызовы — из потока окна: Miniblink однопоточный снаружи.
/// </summary>
internal static unsafe class Miniblink
{
    private const string Library = "miniblink";

    public const int WindowTypeControl = 2;

    /// <summary>Один раз до первого вызова: DLL своей разрядности рядом с exe.</summary>
    public static void UseBundledLibrary() =>
        NativeLibrary.SetDllImportResolver(typeof(Miniblink).Assembly, (name, _, _) =>
            name == Library
                ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, Environment.Is64BitProcess ? "mb132_x64.dll" : "mb132_x32.dll"))
                : IntPtr.Zero);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbInit(void* settings);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbEnableHighDPISupport();
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern IntPtr mbCreateWebWindow(int type, IntPtr parent, int x, int y, int width, int height);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbDestroyWebView(IntPtr view);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbMoveWindow(IntPtr view, int x, int y, int width, int height);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbShowWindow(IntPtr view, int show);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbSetFocus(IntPtr view);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern IntPtr mbGetHostHWND(IntPtr view);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbLoadURL(IntPtr view, byte* url);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbLoadHtmlWithBaseUrl(IntPtr view, byte* html, byte* baseUrl);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern IntPtr mbWebFrameGetMainFrame(IntPtr view);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbRunJs(IntPtr view, IntPtr frame, byte* script, int isInClosure, void* callback, IntPtr param, IntPtr unused);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbResponseQuery(IntPtr view, long queryId, int customMessage, byte* response);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbSetContextMenuEnabled(IntPtr view, int enabled);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbSetNavigationToNewWindowEnable(IntPtr view, int enabled);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbSetDragDropEnable(IntPtr view, int enabled);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbSetCookieJarFullPath(IntPtr view, [MarshalAs(UnmanagedType.LPWStr)] string path);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbSetLocalStorageFullPath(IntPtr view, [MarshalAs(UnmanagedType.LPWStr)] string path);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbNetHookRequest(IntPtr job);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbNetSetData(IntPtr job, byte* data, int length);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbNetSetMIMEType(IntPtr job, byte* type);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbNetCancelRequest(IntPtr job);
    [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern void mbNetSetHTTPHeaderField(IntPtr job, [MarshalAs(UnmanagedType.LPWStr)] string key, [MarshalAs(UnmanagedType.LPWStr)] string value, int response);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    public static extern void mbOnJsQuery(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, long, int, byte*, void> callback, IntPtr param);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    public static extern void mbOnLoadUrlBegin(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, byte*, IntPtr, int> callback, IntPtr param);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    public static extern void mbOnNavigation(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, byte*, int> callback, IntPtr param);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    public static extern void mbOnLoadingFinish(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, byte*, int, byte*, void> callback, IntPtr param);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    public static extern void mbOnConsole(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, byte*, byte*, uint, byte*, void> callback, IntPtr param);
}
