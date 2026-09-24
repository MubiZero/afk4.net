using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Realtime;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Web.WebView2.Core;

namespace AFK4.Player.Shell.Web;

public partial class WebViewPlayerWindow : Window
{
    private readonly PlayerShellOptions options;
    private readonly ShellPipeClient agentPipe;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ShellBridgeHost bridge;
    private readonly DevicePlayerSession session;
    private readonly HttpClient apiHttp;
    private PlayerShellStateDto? latestState;
    private string? appSource;
    private int webViewRestartCount;
    private const int MaxWebViewRestarts = 5;

    public WebViewPlayerWindow()
        : this(
            new PlayerShellOptions
            {
                ShellPipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_PIPE_NAME") ?? ShellPipeProtocol.DefaultPipeName
            })
    {
    }

    internal WebViewPlayerWindow(PlayerShellOptions options)
    {
        this.options = options;
        agentPipe = new ShellPipeClient(options);
        apiHttp = new HttpClient();
        session = new DevicePlayerSession(apiHttp, ApiBaseUrl, TimeProvider.System);
        bridge = new ShellBridgeHost(agentPipe, session, () => latestState);
        bridge.AuthChanged += auth => PostToPage(ShellBridgeEventTypeNames.AuthChanged, auth);
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
            // Адрес API назовёт агент в состоянии — он может отличаться от адреса из конфига хоста.
            // Поэтому смотрим все запросы страницы к серверу, а решает политика по текущему адресу.
            Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Fetch);
            Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.XmlHttpRequest);
            Browser.CoreWebView2.WebResourceRequested += OnApiResourceRequested;
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.NewWindowRequested += (_, windowArgs) => windowArgs.Handled = true;

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
            appSource = target.Source;
            Browser.Source = new Uri(target.Source);

            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _ = agentPipe.RunAsync(lifetime.Token);
            _ = ListenForStateAsync(lifetime.Token);
            _ = ListenForPushesAsync(lifetime.Token);
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
            Browser.CoreWebView2?.PostWebMessageAsJson(responseJson);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // Обработчик async void без общей страховки: исключение отсюда уронило бы киоск, и агент
            // перезапускал бы его по кругу. Пишем в журнал и живём дальше.
            PlayerShellStartupLog.Write("Player Shell host bridge message failed.", exception);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!ShellNavigationPolicy.IsAllowed(e.Uri, appSource))
        {
            e.Cancel = true;
        }
    }

    private void PostToPage(string eventType, object? payload)
    {
        var message = ShellBridgeHost.Event(eventType, payload);
        Dispatcher.InvokeAsync(() => Browser.CoreWebView2?.PostWebMessageAsJson(message));
    }

    /// <summary>
    /// Кадры агента без запроса: вход игрока (ПИН-код или QR) и команды клуба. Сообщение клуба
    /// показывает окно поверх игры — это следующий срез хоста; до него оно только в журнале.
    /// </summary>
    private async Task ListenForPushesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in agentPipe.ReadPushesAsync(cancellationToken))
            {
                if (frame is { Type: ShellPipeMessageTypeNames.Auth, Auth: { } signedIn })
                {
                    PostToPage(ShellBridgeEventTypeNames.AuthChanged, session.Accept(signedIn));
                }
                else if (frame is { Type: ShellPipeMessageTypeNames.Command, Command: { } command })
                {
                    if (command.Type == DeviceCommandTypeNames.SignOut)
                    {
                        // Сервер уже погасил токены — идти к нему незачем, только забыть и сказать странице.
                        session.Forget();
                        PostToPage(ShellBridgeEventTypeNames.AuthChanged, session.Current);
                    }
                    else
                    {
                        PlayerShellStartupLog.Write($"Club command '{command.Type}' reached the shell; the overlay that shows it comes with the next host slice.");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ListenForStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var state in agentPipe.ReadStatesAsync(cancellationToken))
            {
                latestState = state;
                PostToPage(ShellBridgeEventTypeNames.StateChanged, state);
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
        var decision = AuthorizationHeaderPolicy.Decide(e.Request.Uri, ApiBaseUrl(), session.AccessToken);
        if (decision.ShouldInject)
        {
            e.Request.Headers.SetHeader("Authorization", decision.HeaderValue!);
        }
    }

    /// <summary>Адрес API — тот, что назвал агент; до первого состояния — из конфига хоста.</summary>
    private string ApiBaseUrl() =>
        string.IsNullOrWhiteSpace(latestState?.ApiBaseUrl) ? options.ApiBaseUrl : latestState.ApiBaseUrl;

    private async Task RefreshAuthLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                // Сервер отказал в обновлении — вход кончился (сессия закончилась, клуб вывел игрока).
                // Без этого события страница рисовала бы вошедшего, чьи запросы сыплют 401.
                if (await session.EnsureFreshAsync(ct))
                {
                    PostToPage(ShellBridgeEventTypeNames.AuthChanged, session.Current);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
