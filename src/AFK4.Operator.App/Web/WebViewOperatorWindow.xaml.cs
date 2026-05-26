using System.Text.Json;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

        InitializeComponent();
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
        }

        base.OnClosed(e);
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            var launchTarget = assetResolver.Resolve(shellOptions);
            StatusText.Text = $"Loading {launchTarget.Mode}...";

            var userDataFolder = OperatorWebView2UserDataFolder.EnsureExists();
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Browser.EnsureCoreWebView2Async(webViewEnvironment);
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

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        ShowStartupFailure($"Operator UI navigation failed: {e.WebErrorStatus}");
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
        StatusTitle.Text = "Operator UI failed to start";
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
