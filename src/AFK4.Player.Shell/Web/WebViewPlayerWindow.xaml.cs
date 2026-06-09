using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Realtime;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Web.WebView2.Core;

namespace AFK4.Player.Shell.Web;

public partial class WebViewPlayerWindow : Window
{
    private readonly PlayerShellOptions options;
    private readonly IPlayerShellStateClient stateClient;
    private readonly CancellationTokenSource lifetime = new();
    private readonly PlayerShellWebHostBridge bridge;
    private readonly PlayerApiAuthClient authClient;
    private readonly HttpClient apiHttp;
    private PlayerShellStateDto? latestState;

    public WebViewPlayerWindow()
        : this(
            new PlayerShellOptions
            {
                PipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_PIPE_NAME") ?? "afk4-player-shell",
                CommandPipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_COMMAND_PIPE_NAME") ?? "afk4-player-shell-commands"
            })
    {
    }

    internal WebViewPlayerWindow(PlayerShellOptions options)
    {
        this.options = options;
        stateClient = new NamedPipePlayerShellStateClient(options);
        apiHttp = new HttpClient { BaseAddress = new Uri(options.ApiBaseUrl) };
        authClient = new PlayerApiAuthClient(apiHttp);
        bridge = new PlayerShellWebHostBridge(new LauncherCommandClient(options), getLatestState: () => latestState, authClient);
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Browser.EnsureCoreWebView2Async();
        HardenForKiosk(Browser.CoreWebView2);

        var apiBase = options.ApiBaseUrl.TrimEnd('/');
        Browser.CoreWebView2.AddWebResourceRequestedFilter(apiBase + "/*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceRequested += OnApiResourceRequested;

        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: Environment.GetEnvironmentVariable("AFK4_PLAYER_WEB_DEV_SERVER_URL"),
            distIndexHtmlPath: ResolveDistIndexHtml());

        if (target.Kind == PlayerWebLaunchKind.LocalFolder)
        {
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                PlayerWebAssetResolver.LocalVirtualHost,
                target.LocalFolderPath!,
                CoreWebView2HostResourceAccessKind.Allow);
        }

        Browser.CoreWebView2.ProcessFailed += OnProcessFailed;
        Browser.Source = new Uri(target.Source);

        Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _ = ListenForStateAsync(lifetime.Token);
        _ = RefreshAuthLoopAsync(lifetime.Token);
    }

    private static void HardenForKiosk(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        var action = WebViewWatchdogPolicy.Decide(new WebViewHealthSignal(ProcessFailed: true, Unresponsive: false));
        ApplyWatchdog(action);
    }

    private void ApplyWatchdog(WebViewWatchdogAction action)
    {
        FallbackPanel.Visibility = action.ShowFallback ? Visibility.Visible : Visibility.Collapsed;
        FallbackTimer.Text = RemainingTimeFormatterText();
        FallbackMessage.Text = "Восстанавливаем соединение…";

        if (action.RestartWebView)
        {
            Browser.Reload();
            FallbackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private string RemainingTimeFormatterText() =>
        Shell.RemainingTimeFormatter.Format(latestState?.RemainingSeconds);

    private static string? ResolveDistIndexHtml()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "WebAssets", "index.html");
        return File.Exists(candidate) ? candidate : candidate;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var responseJson = await bridge.HandleAsync(e.WebMessageAsJson, lifetime.Token);
        if (responseJson is not null && Browser.CoreWebView2 is not null)
        {
            Browser.CoreWebView2.PostWebMessageAsJson(responseJson);
            Browser.CoreWebView2.PostWebMessageAsJson(PlayerShellWebHostBridge.CreateAuthPush(authClient.Current));
        }
    }

    private async Task ListenForStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var state in stateClient.ReadStatesAsync(cancellationToken))
            {
                latestState = state;
                await Dispatcher.InvokeAsync(() =>
                {
                    Browser.CoreWebView2?.PostWebMessageAsJson(PlayerShellWebHostBridge.CreateStatePush(state));
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        lifetime.Cancel();
        lifetime.Dispose();
        apiHttp.Dispose();
    }

    private void OnApiResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var decision = AuthorizationHeaderPolicy.Decide(e.Request.Uri, options.ApiBaseUrl, authClient.CurrentAccessToken);
        if (decision.ShouldInject)
        {
            e.Request.Headers.SetHeader("Authorization", decision.HeaderValue!);
        }
    }

    private async Task RefreshAuthLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
                await authClient.EnsureFreshTokenAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
