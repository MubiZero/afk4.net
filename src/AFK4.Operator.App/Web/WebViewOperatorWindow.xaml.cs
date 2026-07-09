using System.ComponentModel;
using System.Text.Json;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AFK4.Localization;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Configuration;
using AFK4.Operator.App.Connection;
using Microsoft.Web.WebView2.Core;

namespace AFK4.Operator.App.Web;

public partial class WebViewOperatorWindow : Window
{
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int WmSysCommand = 0x0112;
    private const int HtCaption = 2;
    private const int ScSize = 0xF000;
    private const int WmszLeft = 1;
    private const int WmszRight = 2;
    private const int WmszTop = 3;
    private const int WmszTopLeft = 4;
    private const int WmszTopRight = 5;
    private const int WmszBottom = 6;
    private const int WmszBottomLeft = 7;
    private const int WmszBottomRight = 8;

    private readonly OperatorAppOptions appOptions;
    private readonly OperatorWebShellOptions shellOptions;
    private readonly OperatorWebAssetResolver assetResolver;
    private readonly OperatorWebHostBridge hostBridge;
    private readonly ILocalizationService localization;
    private bool browserInitializationStarted;

    public WebViewOperatorWindow()
        : this(OperatorAppOptions.LoadFromEnvironment())
    {
    }

    private WebViewOperatorWindow(OperatorAppOptions appOptions)
        : this(
            appOptions,
            OperatorWebShellOptions.LoadFromEnvironment(),
            new OperatorWebAssetResolver(AppContext.BaseDirectory),
            CreateDefaultHostBridge(appOptions))
    {
    }

    public WebViewOperatorWindow(
        OperatorAppOptions appOptions,
        OperatorWebShellOptions shellOptions,
        OperatorWebAssetResolver assetResolver)
        : this(
            appOptions,
            shellOptions,
            assetResolver,
            CreateDefaultHostBridge(appOptions))
    {
    }

    public WebViewOperatorWindow(
        OperatorAppOptions appOptions,
        OperatorWebShellOptions shellOptions,
        OperatorWebAssetResolver assetResolver,
        OperatorWebHostBridge hostBridge)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        ArgumentNullException.ThrowIfNull(shellOptions);
        ArgumentNullException.ThrowIfNull(assetResolver);
        ArgumentNullException.ThrowIfNull(hostBridge);

        this.appOptions = appOptions;
        this.shellOptions = shellOptions;
        this.assetResolver = assetResolver;
        this.hostBridge = hostBridge;
        localization = LocalizationService.LoadEmbedded(appOptions.PreferredLocale);

        InitializeComponent();

        StatusText.Text = localization.T("operator.host.loading");
        RestoreSavedPlacement();
    }

    // Re-open the window at the size/position/maximized-state the operator last left it. Applied in the
    // constructor (before the window is shown) so there is no visible jump from the default geometry.
    private void RestoreSavedPlacement()
    {
        var saved = OperatorWindowPlacementStore.Load();
        var sane = saved?.Sanitize(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight,
            MinWidth,
            MinHeight);
        if (sane is null)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = sane.Left;
        Top = sane.Top;
        Width = sane.Width;
        Height = sane.Height;

        // The restore bounds above become the un-maximize size; WM_GETMINMAXINFO clamps the maximized
        // rect to the work area, so re-maximizing here is safe on any monitor.
        if (saved!.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ApplyModernWindowChrome(handle);

        // Borderless (WindowStyle=None) windows maximize to the full monitor rect plus the invisible
        // resize border, so they spill ~8px off every screen edge (content clipped) and cover the
        // taskbar. Clamp the maximized bounds to the monitor work area via WM_GETMINMAXINFO.
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    // Borderless windows lose the OS-managed rounded corners, drop shadow and crisp edge. Faking
    // corners with a XAML CornerRadius leaves the real window pixels square (dark corner triangles +
    // no shadow), so let DWM round/clip the whole window — including the WebView2 child — and draw a
    // 1px contour. No-ops on pre-Win11; failures are intentionally ignored.
    private static void ApplyModernWindowChrome(IntPtr handle)
    {
        var cornerPreference = DwmwcpRound;
        DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

        var borderColor = ContourBorderColor;
        DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(int));
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ClampWindowTrackSize(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    // Owns WM_GETMINMAXINFO to (1) clamp the maximized rect to the monitor work area and (2) enforce the
    // window's minimum size. Both must be set here: because we mark the message handled, WindowChrome no
    // longer pushes MinWidth/MinHeight into ptMinTrackSize, so without (2) the border drags far below the
    // minimum and the WebView2 content clips instead of the window simply refusing to shrink further.
    private void ClampWindowTrackSize(IntPtr hwnd, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
        {
            var work = monitorInfo.WorkArea;
            var bounds = monitorInfo.MonitorArea;
            info.MaxPosition.X = work.Left - bounds.Left;
            info.MaxPosition.Y = work.Top - bounds.Top;
            info.MaxSize.X = work.Right - work.Left;
            info.MaxSize.Y = work.Bottom - work.Top;
        }

        // MinWidth/MinHeight are DIPs; ptMinTrackSize is physical pixels — scale by the window's DPI.
        var dpiScale = DpiScaleForWindow(hwnd);
        info.MinTrackSize.X = (int)Math.Ceiling(MinWidth * dpiScale);
        info.MinTrackSize.Y = (int)Math.Ceiling(MinHeight * dpiScale);

        Marshal.StructureToPtr(info, lParam, true);
    }

    private static double DpiScaleForWindow(IntPtr hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (browserInitializationStarted)
        {
            return;
        }

        browserInitializationStarted = true;
        await InitializeBrowserAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        SaveCurrentPlacement();
    }

    // Persist the geometry to restore next launch. When maximized we save RestoreBounds (the normal-state
    // size to fall back to) plus the maximized flag; a minimized window keeps its restore bounds too.
    private void SaveCurrentPlacement()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        OperatorWindowPlacementStore.Save(new OperatorWindowPlacement
        {
            Left = bounds.X,
            Top = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == WindowState.Maximized,
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        Browser.NavigationCompleted -= OnNavigationCompleted;
        if (Browser.CoreWebView2 is not null)
        {
            Browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            Browser.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            Browser.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
        }

        base.OnClosed(e);
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            // In a packaged/normal run an unprovisioned platform URL still defaults to localhost,
            // which would silently load a dead UI. Surface a clear "not configured" failure instead.
            // The dev harness (vite dev server env set) legitimately runs against localhost, so skip
            // the guard there.
            if (!IsDevShell && appOptions.PlatformBaseUrl.IsLoopback)
            {
                ShowStartupFailure(localization.T("operator.host.notConfigured"));
                return;
            }

            var launchTarget = assetResolver.Resolve(shellOptions);
            StatusText.Text = localization.T("operator.host.loading");

            var userDataFolder = OperatorWebView2UserDataFolder.EnsureExists();
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Browser.EnsureCoreWebView2Async(webViewEnvironment);
            HardenWebView(Browser.CoreWebView2);
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            if (launchTarget.UsesLocalFolder)
            {
                Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    OperatorWebAssetResolver.LocalVirtualHost,
                    launchTarget.LocalFolderPath!,
                    CoreWebView2HostResourceAccessKind.Allow);
            }

            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                OperatorWebBootstrapScript.Create(appOptions, launchTarget));

            Browser.Source = launchTarget.Source;
        }
        catch (Exception exception)
        {
            ShowStartupFailure(exception.Message);
        }
    }

    // The dev harness runs the operator UI from a localhost vite dev server (env-provisioned).
    // It's the one legitimate localhost scenario and also where DevTools / dev-origin navigation
    // must stay allowed.
    private bool IsDevShell => shellOptions.DevServerUrl is not null;

    // Operators interact with the app (not a full kiosk), so this is conservative: lock down the
    // token-exfil / navigate-away vectors on shared staff PCs without breaking the legit UI.
    // API calls are fetch/XHR (not navigations), so NavigationStarting never touches them.
    private void HardenWebView(CoreWebView2 core)
    {
        if (!IsDevShell)
        {
            core.Settings.AreDevToolsEnabled = false;
        }

        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target))
        {
            e.Cancel = true;
            return;
        }

        // Only http(s) navigations are the navigate-away vector. Leave internal schemes
        // (about:blank, data:, etc.) alone so framework-internal navigation isn't broken.
        if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        if (string.Equals(target.Host, OperatorWebAssetResolver.LocalVirtualHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (IsDevShell && shellOptions.DevServerUrl is { } devServer &&
            string.Equals(target.Host, devServer.Host, StringComparison.OrdinalIgnoreCase) &&
            target.Port == devServer.Port)
        {
            return;
        }

        // Anything else (navigate-away to an arbitrary site) is blocked.
        e.Cancel = true;
    }

    private static void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // No popups / new windows from the operator UI.
        e.Handled = true;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        ShowStartupFailure(string.Format(localization.T("operator.host.navFailed"), e.WebErrorStatus));
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // A throw from this async-void handler escapes into the dispatcher and crashes the host.
        // Log it and return a structured host:response error so the React side fails gracefully.
        try
        {
            if (TryHandleWindowMessage(e.WebMessageAsJson))
            {
                return;
            }

            var responseJson = await hostBridge.HandleAsync(e.WebMessageAsJson, CancellationToken.None);
            if (responseJson is not null && Browser.CoreWebView2 is not null)
            {
                Browser.CoreWebView2.PostWebMessageAsJson(responseJson);
            }
        }
        catch (Exception exception)
        {
            OperatorStartupLog.Write("Unhandled error in operator host bridge.", exception);

            var errorResponse = TryCreateBridgeErrorResponse(e.WebMessageAsJson, exception);
            if (errorResponse is not null && Browser.CoreWebView2 is not null)
            {
                Browser.CoreWebView2.PostWebMessageAsJson(errorResponse);
            }
        }
    }

    private static string? TryCreateBridgeErrorResponse(string webMessageJson, Exception exception)
    {
        try
        {
            using var document = JsonDocument.Parse(webMessageJson);
            if (!document.RootElement.TryGetProperty("requestId", out var requestIdProperty) ||
                requestIdProperty.GetString() is not { Length: > 0 } requestId)
            {
                return null;
            }

            var response = new
            {
                type = "host:response",
                requestId,
                ok = false,
                payload = (object?)null,
                error = new { code = "host_error", message = exception.Message }
            };

            return JsonSerializer.Serialize(response);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool TryHandleWindowMessage(string webMessageJson)
    {
        try
        {
            using var document = JsonDocument.Parse(webMessageJson);
            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                return false;
            }

            switch (typeProperty.GetString())
            {
                case "window:drag":
                    StartNativeWindowDrag();
                    break;
                case "window:resize":
                    if (document.RootElement.TryGetProperty("edge", out var edgeProperty))
                    {
                        StartNativeWindowResize(edgeProperty.GetString());
                    }
                    break;
                case "window:minimize":
                    WindowState = WindowState.Minimized;
                    break;
                case "window:maximize":
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                    break;
                case "window:close":
                    Close();
                    break;
                case "window:theme":
                    ApplyIconForTheme(document.RootElement.TryGetProperty("theme", out var themeProperty)
                        ? themeProperty.GetString()
                        : null);
                    break;
                default:
                    return false;
            }

            return true;
        }
        catch (JsonException)
        {
            // Ignore malformed web messages. The bridge is intentionally narrow.
            return true;
        }
    }

    // Тёмная тема оператора (дефолт) — таскбар обычно тоже тёмный, поэтому светлая иконка
    // (белый фон) заметнее; светлая тема оператора — наоборот, тёмная иконка контрастнее.
    private static readonly Uri LightIconUri = new("pack://application:,,,/Assets/afk4-icon-light.png");
    private static readonly Uri DarkIconUri = new("pack://application:,,,/Assets/afk4-icon-dark.png");

    private void ApplyIconForTheme(string? theme)
    {
        Icon = new BitmapImage(theme == "light" ? DarkIconUri : LightIconUri);
    }

    private void StartNativeWindowDrag()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(handle, WmNcLeftButtonDown, HtCaption, 0);
    }

    private void StartNativeWindowResize(string? edge)
    {
        if (ResizeMode is ResizeMode.NoResize or ResizeMode.CanMinimize)
        {
            return;
        }

        var resizeDirection = edge switch
        {
            "left" => WmszLeft,
            "right" => WmszRight,
            "top" => WmszTop,
            "top-left" => WmszTopLeft,
            "top-right" => WmszTopRight,
            "bottom" => WmszBottom,
            "bottom-left" => WmszBottomLeft,
            "bottom-right" => WmszBottomRight,
            _ => 0
        };
        if (resizeDirection == 0)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(handle, WmSysCommand, ScSize + resizeDirection, 0);
    }

    private void ShowStartupFailure(string message)
    {
        StatusTitle.Text = localization.T("operator.host.failedTitle");
        StatusText.Text = message;
        StartupOverlay.Visibility = Visibility.Visible;
    }

    private static OperatorWebHostBridge CreateDefaultHostBridge(OperatorAppOptions appOptions)
    {
        ArgumentNullException.ThrowIfNull(appOptions);

        var tokenStore = new ProtectedDataOperatorTokenStore();
        var connectionStore = new ProtectedDataOperatorConnectionStore();
        var httpClient = new HttpClient
        {
            BaseAddress = appOptions.PlatformBaseUrl
        };

        return new OperatorWebHostBridge(
            new HttpOperatorAuthApiClient(httpClient, tokenStore),
            tokenStore,
            connectionStore);
    }

    // DWM window-attribute ids (Windows 11 22000+): 33 = corner preference, 34 = border colour.
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpRound = 2;

    // COLORREF is 0x00BBGGRR. #1B2330 — a dim neutral barely above the #0E1117 surface, so the edge
    // reads as a thin grain rather than a bright frame.
    private const int ContourBorderColor = 0x0030231B;

    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
