using System.Text.Json;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
