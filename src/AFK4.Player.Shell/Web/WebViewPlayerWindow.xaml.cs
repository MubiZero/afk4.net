using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using AFK4.Localization;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Input;
using AFK4.Player.Shell.Kiosk;
using AFK4.Player.Shell.Overlay;
using AFK4.Player.Shell.Workstation;
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
    private readonly InputActivityTracker input = new(InputActivityTracker.DefaultIdleAfter, InputActivityTracker.DefaultActivityEvery);
    private readonly GameForegroundTracker game = new(GameForegroundTracker.DefaultSettle);
    private readonly LocalizationService localization = LocalizationService.LoadEmbedded("ru");
    private readonly DispatcherTimer tick = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    private PlayerShellStateDto? latestState;
    private long stateReceivedAt;
    private string? appSource;
    private readonly WindowsSystemControls systemControls = new();
    private readonly KioskKeyboardHook keyboard = new();
    private readonly WindowBlocker windows = new();
    private readonly MaintenanceBand band;
    private OverlayWindow? overlay;
    private ClubMessage? clubMessage;
    private ShellSystemStateDto? lastSystem;
    private int tickCount;
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
        band = new MaintenanceBand(this);
        agentPipe = new ShellPipeClient(options);
        apiHttp = new HttpClient();
        session = new DevicePlayerSession(apiHttp, ApiBaseUrl, TimeProvider.System);
        bridge = new ShellBridgeHost(agentPipe, session, () => latestState, systemControls);
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

            MapShowcaseAssets();

            Browser.CoreWebView2.ProcessFailed += OnProcessFailed;
            appSource = target.Source;
            Browser.Source = new Uri(target.Source);

            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _ = agentPipe.RunAsync(lifetime.Token);
            _ = ListenForStateAsync(lifetime.Token);
            _ = ListenForPushesAsync(lifetime.Token);
            _ = RefreshAuthLoopAsync(lifetime.Token);

            // Пока агент молчит, ПК заперт: перехват стоит с первой секунды, а не с первого состояния.
            keyboard.Start();
            windows.Start();

            overlay = new OverlayWindow(localization);
            overlay.ExtendRequested += BringShellForward;
            tick.Tick += OnTick;
            tick.Start();
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

    /// <summary>
    /// За ПК кто-то есть — агенту, не чаще раза в 20 секунд: по этому он не выключит простаивающий
    /// ПК под рукой человека и отменит выключение, назначенное за минуту. Ответ не ждём.
    /// </summary>
    private void ReportPresence()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - lastPresenceReported < TimeSpan.FromSeconds(20))
        {
            return;
        }

        lastPresenceReported = now;
        _ = agentPipe.RequestAsync(ShellPipeRequestTypeNames.Activity, new Dictionary<string, string>(), lifetime.Token);
    }

    private DateTimeOffset lastPresenceReported = DateTimeOffset.MinValue;

    /// <summary>
    /// Обложки игр (и витрина P7) — из общей папки ПК, которую пополняет агент. Только на чтение:
    /// страница показывает картинки, но ничего туда не пишет. Папки ещё нет — агент её не завёл, и
    /// плитки рисуются по названию.
    /// </summary>
    private void MapShowcaseAssets()
    {
        var folder = AFK4.Shared.Contracts.Shell.ShellShowcaseAssets.Directory();
        if (!System.IO.Directory.Exists(folder))
        {
            return;
        }

        Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
            AFK4.Shared.Contracts.Shell.ShellShowcaseAssets.VirtualHost,
            folder,
            CoreWebView2HostResourceAccessKind.Allow);
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
    /// показывает окно поверх игры.
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
                    else if (command.Type == DeviceCommandTypeNames.Message && !string.IsNullOrWhiteSpace(command.Text))
                    {
                        var message = new ClubMessage(command.Text, DateTimeOffset.UtcNow);
                        await Dispatcher.InvokeAsync(() => clubMessage = message);
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
                PostToPage(ShellBridgeEventTypeNames.StateChanged, state);
                await Dispatcher.InvokeAsync(() =>
                {
                    latestState = state;
                    stateReceivedAt = Stopwatch.GetTimestamp();
                    ApplyWindowLayer(state);
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Четыре раза в секунду: ввод, переднее окно и окно поверх игры; раз в секунду — звук и раскладка. Опрос дешевле хука и не
    /// зависит от того, в каком потоке Windows решит его вызвать; решения — в чистых классах.
    /// </summary>
    private async void OnTick(object? sender, EventArgs e)
    {
        try
        {
            switch (input.Observe(NativeInput.LastInputTick(), NativeInput.NowTick()))
            {
                case InputSignal.Activity:
                    PostToPage(ShellBridgeEventTypeNames.InputActivity, null);
                    ReportPresence();
                    break;
                case InputSignal.Idle:
                    PostToPage(ShellBridgeEventTypeNames.InputIdle, null);
                    break;
            }

            var now = DateTimeOffset.UtcNow;
            var shellInFront = NativeInput.ShellInFront();
            keyboard.SetShellInFront(shellInFront);
            if (game.Observe(shellInFront, latestState, now) is { } gameActive)
            {
                await SetPageAsleepAsync(gameActive);
            }

            // Звук, микрофон и раскладку игрок мог поменять клавишами — раз в секунду сверяемся.
            if (++tickCount % 4 == 0)
            {
                var systemNow = systemControls.Read();
                if (systemNow != lastSystem)
                {
                    lastSystem = systemNow;
                    PostToPage(ShellBridgeEventTypeNames.SystemChanged, systemNow);
                }
            }

            localization.SetLocale(bridge.Locale ?? latestState?.Locale ?? "ru");
            overlay?.Present(OverlayPresenter.Decide(latestState, RemainingSecondsNow(), shellInFront, clubMessage, now));
        }
        catch (Exception exception)
        {
            // Таймер — async void: сбой одного круга не должен уронить киоск.
            PlayerShellStartupLog.Write("Player Shell tick failed.", exception);
        }
    }

    /// <summary>Остаток по часам хоста: сколько прошло с прихода состояния, а не по часам платформы.</summary>
    private int? RemainingSecondsNow() =>
        latestState?.RemainingSeconds is { } remaining
            ? remaining - (int)Stopwatch.GetElapsedTime(stateReceivedAt).TotalSeconds
            : null;

    /// <summary>
    /// «Поверх всех» только на запертом экране; при блокировке окно возвращается наверх. В
    /// обслуживании — полоса сверху, под ней рабочий стол техника.
    /// </summary>
    private void ApplyWindowLayer(PlayerShellStateDto state)
    {
        keyboard.SetMode(KeyboardBlockPolicy.ModeFor(state));
        windows.SetRules(state.BlockedWindows);
        var layout = ShellWindowPolicy.Layout(state);
        if (layout == ShellWindowLayout.Band)
        {
            if (!band.IsDocked)
            {
                // Полоса — не заслон: поверх окон техника она стоит как панель задач, а не как киоск.
                Topmost = true;
                band.Dock(ShellWindowPolicy.BandHeight);
            }

            return;
        }

        if (band.IsDocked)
        {
            band.Undock();
            WindowState = WindowState.Maximized;
        }

        var onTop = layout == ShellWindowLayout.Cover;
        if (Topmost == onTop)
        {
            return;
        }

        Topmost = onTop;
        if (onTop)
        {
            Activate();
        }
    }

    /// <summary>«Продлить» в окне поверх игры: оболочка выходит вперёд, продление подтверждают там.</summary>
    private void BringShellForward()
    {
        Topmost = true;
        Activate();
        Topmost = ShellWindowPolicy.ShouldStayOnTop(latestState);
    }

    /// <summary>
    /// Игра впереди — страница засыпает (спека оболочки, §8): невидима, приостановлена, память —
    /// на минимуме. Вернулись — просыпается до того, как игрок её увидит.
    /// </summary>
    private async Task SetPageAsleepAsync(bool asleep)
    {
        var core = Browser.CoreWebView2;
        if (asleep)
        {
            PostToPage(ShellBridgeEventTypeNames.GameForeground, new ShellGameForegroundDto(true));
            Browser.Visibility = Visibility.Hidden;
            if (core is not null)
            {
                core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                await core.TrySuspendAsync();
            }

            return;
        }

        if (core is not null)
        {
            core.Resume();
            core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
        }

        Browser.Visibility = Visibility.Visible;
        PostToPage(ShellBridgeEventTypeNames.GameForeground, new ShellGameForegroundDto(false));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        tick.Stop();
        keyboard.Dispose();
        windows.Dispose();
        band.Dispose();
        overlay?.Close();
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
