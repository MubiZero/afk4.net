using System.IO;
using System.Net.Http;
using System.Text.Json;
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
    private int webViewRestartCount;
    private const int MaxWebViewRestarts = 5;

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
        try
        {
            var userDataFolder = PlayerWebView2UserDataFolder.EnsureExists();
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Browser.EnsureCoreWebView2Async(webViewEnvironment);
            HardenForKiosk(Browser.CoreWebView2);

            var apiBase = options.ApiBaseUrl.TrimEnd('/');
            Browser.CoreWebView2.AddWebResourceRequestedFilter(apiBase + "/*", CoreWebView2WebResourceContext.All);
            Browser.CoreWebView2.WebResourceRequested += OnApiResourceRequested;

            // Hand the web layer the SAME origin the host signs tokens for, at runtime. Otherwise the
            // web's build-time VITE_PLATFORM_API_BASE_URL can point at a different API than the host
            // injects the bearer for (e.g. shipped staging build pointed at a prod host) -> every
            // /api/me/* call loses its token and 401s.
            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                $"window.__AFK4_PLAYER_CONFIG__ = {{ \"platformBaseUrl\": {JsonSerializer.Serialize(apiBase)} }};");
            Browser.CoreWebView2.NavigationCompleted += (_, navArgs) =>
            {
                // A successful load means the renderer is healthy again; reset the watchdog budget.
                if (navArgs.IsSuccess)
                {
                    webViewRestartCount = 0;
                }
            };

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
        catch (Exception exception)
        {
            // A throw from this async-void handler would crash the kiosk window, and the agent
            // would relaunch it in a tight loop with no record of why. Log and keep the window up.
            PlayerShellStartupLog.Write("WebView startup failed in OnLoaded.", exception);
            ApplyWatchdog(WebViewWatchdogPolicy.Decide(new WebViewHealthSignal(ProcessFailed: true, Unresponsive: false)));
        }
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
        try
        {
            // A renderer/GPU crash is recoverable by reload; a browser-process exit invalidates
            // CoreWebView2 so a plain Reload() would itself throw. Cap restarts so a persistently
            // broken renderer (bad driver/OOM on cheap club PCs) shows a stable fallback instead of
            // a tight reload storm. The counter is reset on any successful navigation.
            var recoverable = e.ProcessFailedKind is
                CoreWebView2ProcessFailedKind.RenderProcessExited or
                CoreWebView2ProcessFailedKind.RenderProcessUnresponsive or
                CoreWebView2ProcessFailedKind.FrameRenderProcessExited;

            if (!recoverable || webViewRestartCount >= MaxWebViewRestarts)
            {
                PlayerShellStartupLog.Write(
                    $"WebView process failed ({e.ProcessFailedKind}); restart {webViewRestartCount}/{MaxWebViewRestarts}. Showing fallback.");
                ApplyWatchdog(new WebViewWatchdogAction(ShowFallback: true, RestartWebView: false));
                return;
            }

            webViewRestartCount++;
            ApplyWatchdog(new WebViewWatchdogAction(ShowFallback: true, RestartWebView: true));
        }
        catch (Exception exception)
        {
            // The handler is a void event; an escape would crash the kiosk and trigger an
            // agent-supervised relaunch loop. Log and leave the fallback panel up.
            PlayerShellStartupLog.Write("WebView watchdog failed handling ProcessFailed.", exception);
        }
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

    private static string? ResolveDistIndexHtml() =>
        // Where the packaged build lands. The resolver prefers a dev-server URL when one is set
        // (checked first), otherwise serves this path via the virtual host; in production the file
        // is always present, and a failed load is covered by the watchdog/native fallback.
        Path.Combine(AppContext.BaseDirectory, "WebAssets", "index.html");

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var responseJson = await bridge.HandleAsync(e.WebMessageAsJson, lifetime.Token);
            if (responseJson is not null && Browser.CoreWebView2 is not null)
            {
                Browser.CoreWebView2.PostWebMessageAsJson(responseJson);

                // Only a sign-in/sign-out changes auth state, so only those warrant a push; pushing on
                // every message (e.g. loadState, launch) would spam shell:authChanged with no change.
                if (IsAuthMutation(e.WebMessageAsJson))
                {
                    Browser.CoreWebView2.PostWebMessageAsJson(PlayerShellWebHostBridge.CreateAuthPush(authClient.Current));
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // The bridge can throw on a transient network blip (auth HttpRequestException) or a
            // launcher pipe failure. This is an async-void handler with no global backstop, so an
            // escape crashes the kiosk and the agent relaunches it in a loop. Log and stay up.
            PlayerShellStartupLog.Write("Player Shell host bridge message failed.", exception);
        }
    }

    private static bool IsAuthMutation(string requestJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
            return type is "auth:signIn" or "auth:signOut";
        }
        catch (JsonException)
        {
            return false;
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
                var wasAuthenticated = authClient.Current.Authenticated;
                await authClient.EnsureFreshTokenAsync(ct);

                // If a definitive 401 just signed the player out, tell the web app so it drops to
                // the login screen. Without this push the UI keeps showing a signed-in player whose
                // every /api/me/* call silently 401s.
                if (wasAuthenticated && !authClient.Current.Authenticated)
                {
                    await Dispatcher.InvokeAsync(() =>
                        Browser.CoreWebView2?.PostWebMessageAsJson(
                            PlayerShellWebHostBridge.CreateAuthPush(authClient.Current)));
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
