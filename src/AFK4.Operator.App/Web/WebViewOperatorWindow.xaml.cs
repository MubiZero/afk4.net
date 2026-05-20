using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AFK4.Operator.App.Configuration;
using Microsoft.Web.WebView2.Core;

namespace AFK4.Operator.App.Web;

public partial class WebViewOperatorWindow : Window
{
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 2;

    private readonly OperatorAppOptions appOptions;
    private readonly OperatorWebShellOptions shellOptions;
    private readonly OperatorWebAssetResolver assetResolver;
    private bool browserInitializationStarted;

    public WebViewOperatorWindow()
        : this(
            OperatorAppOptions.LoadFromEnvironment(),
            OperatorWebShellOptions.LoadFromEnvironment(),
            new OperatorWebAssetResolver(AppContext.BaseDirectory))
    {
    }

    public WebViewOperatorWindow(
        OperatorAppOptions appOptions,
        OperatorWebShellOptions shellOptions,
        OperatorWebAssetResolver assetResolver)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        ArgumentNullException.ThrowIfNull(shellOptions);
        ArgumentNullException.ThrowIfNull(assetResolver);

        this.appOptions = appOptions;
        this.shellOptions = shellOptions;
        this.assetResolver = assetResolver;

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

            await Browser.EnsureCoreWebView2Async();
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

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            if (!document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                return;
            }

            switch (typeProperty.GetString())
            {
                case "window:drag":
                    StartNativeWindowDrag();
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
            }
        }
        catch (JsonException)
        {
            // Ignore malformed web messages. The bridge is intentionally narrow.
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

    private void ShowStartupFailure(string message)
    {
        StatusTitle.Text = "Operator UI failed to start";
        StatusText.Text = message;
        StartupOverlay.Visibility = Visibility.Visible;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
