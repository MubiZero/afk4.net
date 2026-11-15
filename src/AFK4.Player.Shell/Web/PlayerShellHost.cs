using System.Buffers;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AFK4.Localization;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Input;
using AFK4.Player.Shell.Kiosk;
using AFK4.Player.Shell.Overlay;
using AFK4.Player.Shell.Workstation;
using AFK4.Player.Shell.Realtime;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Net;
using AFK4.Shared.Contracts.Shell;
using AFK4.WebHost;

namespace AFK4.Player.Shell.Web;

/// <summary>
/// Хост экрана игрока: окно киоска на Miniblink (AFK4.WebHost, Native AOT — docs/roadmap/windows7.md), мост к странице,
/// канал агента, перехват клавиатуры и окон, окно поверх игры. Все решения (когда «поверх всех», когда полоса, что
/// показать поверх игры) — в чистых классах рядом; здесь только их склейка с окнами Windows. Всё, что трогает окно и
/// движок, выполняется в потоке окон.
/// </summary>
public sealed class PlayerShellHost : IDisposable
{
    private readonly PlayerShellOptions options;
    private readonly ShellPipeClient agentPipe;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ShellBridgeHost bridge;
    private readonly DevicePlayerSession session;
    private readonly HttpClient apiHttp;
    private readonly InputActivityTracker input = new(InputActivityTracker.DefaultIdleAfter, InputActivityTracker.DefaultActivityEvery);
    private readonly GameForegroundTracker game = new(GameForegroundTracker.DefaultSettle);
    private readonly NativeWindows appWindows = new();
    private readonly LaunchFocusWatcher launchFocus = new(LaunchFocusWatcher.DefaultWatch);
    private readonly EmptyScreenGuard emptyScreen = new(EmptyScreenGuard.DefaultSettle);
    private readonly LocalizationService localization = LocalizationService.LoadEmbedded("ru");
    private readonly WindowsSystemControls systemControls = new();
    private readonly KioskKeyboardHook keyboard = new();
    private readonly WindowBlocker windows = new();
    private WebHostWindow? window;
    private OverlayWindow? overlay;
    private PlayerShellStateDto? latestState;
    private long stateReceivedAt;
    private ClubMessage? clubMessage;
    private ShellSystemStateDto? lastSystem;
    private DateTimeOffset lastPresenceReported = DateTimeOffset.MinValue;
    private bool onTop = true;
    private bool disposed;
    private int tickCount;
    private int loadRestartCount;
    private const int MaxLoadRestarts = 5;

    public PlayerShellHost()
        : this(
            new PlayerShellOptions
            {
                ShellPipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_PIPE_NAME") ?? ShellPipeProtocol.DefaultPipeName
            })
    {
    }

    internal PlayerShellHost(PlayerShellOptions options)
    {
        this.options = options;
        agentPipe = new ShellPipeClient(options);
        apiHttp = new HttpClient(ClubTls.Apply(new SocketsHttpHandler()));
        session = new DevicePlayerSession(apiHttp, ApiBaseUrl, TimeProvider.System);
        bridge = new ShellBridgeHost(agentPipe, session, () => latestState, systemControls, appWindows);
        bridge.AuthChanged += auth => PostToPage(ShellBridgeEventTypeNames.AuthChanged, auth);
        bridge.AppLaunched += () => WebHostWindow.RunOnUiThread(BeginLaunchFocus);
        keyboard.AppsHotkeyPressed += () => WebHostWindow.RunOnUiThread(() => _ = ShowAppsPanelAsync());
    }

    /// <summary>Поднять киоск: перехват, окно с экраном, канал агента, опрос. В потоке окон, после <see cref="WebHostWindow.Initialize"/>.</summary>
    public void Start()
    {
        // Пока агент молчит, ПК заперт: перехват стоит с первой секунды — и до движка. Раньше он ставился после, и сбой
        // движка (битая установка, нет файлов) оставлял киоск без перехвата: Alt+Tab и Win уводили гостя на рабочий стол.
        // Правила закрытия окон приезжают с состоянием, поэтому блокировщик окон до тех пор ничего не трогает — в том
        // числе окна самого движка.
        keyboard.Start();
        windows.Start();

        try
        {
            var target = PlayerWebAssetResolver.Resolve(
                devServerUrl: Environment.GetEnvironmentVariable("AFK4_PLAYER_WEB_DEV_SERVER_URL"),
                distIndexHtmlPath: ResolveDistIndexHtml());
            var showcase = ShellShowcaseAssets.Directory();

            window = new WebHostWindow(new WebHostOptions
            {
                Title = "AFK4.NET Player Shell",
                ContentRoot = target.LocalFolderPath ?? AppContext.BaseDirectory,
                VirtualHost = PlayerWebAssetResolver.LocalVirtualHost,
                DevServerUrl = target.Kind == PlayerWebLaunchKind.DevServer ? new Uri(target.Source) : null,
                // Адрес API — тот же, для которого хост подписывает токены. Иначе адрес, вшитый в сборку страницы, мог
                // бы указывать на другой API (рабочая сборка с адресом боевого): все /api/me/* без токена получили бы 401.
                InitScript = $"window.__AFK4_PLAYER_CONFIG__ = {{ \"platformBaseUrl\": {JsonString(options.ApiBaseUrl.TrimEnd('/'))} }};",
                // Обложки игр и витрина — из общей папки ПК, которую пополняет агент. Только на чтение; папки ещё нет —
                // плитки рисуются по названию.
                ExtraHosts = new Dictionary<string, string> { [ShellShowcaseAssets.VirtualHost] = showcase },
                DataFolder = PlayerShellDataFolder.Resolve(),
                // Токен знает только хост: страница к серверу ходит сама, а заголовок ей ставит окно (токен она не видит).
                RequestAuthorization = AuthorizationFor,
                // Ссылки с экрана киоска никуда не ведут: на запертом ПК браузеру делать нечего.
                OpenExternalLinks = false,
                Kiosk = true
            });
            window.MessageReceived += json => WebHostWindow.RunOnUiThread(() => _ = HandleMessageAsync(json));
            window.Diagnostic += text => PlayerShellStartupLog.Write("Player Shell window: " + text);
            window.PageLoaded += OnPageLoaded;
            window.PageLoadFailed += OnPageLoadFailed;
            window.Show();

            _ = agentPipe.RunAsync(lifetime.Token);
            _ = ListenForStateAsync(lifetime.Token);
            _ = ListenForPushesAsync(lifetime.Token);
            _ = RefreshAuthLoopAsync(lifetime.Token);

            overlay = new OverlayWindow(localization);
            overlay.ExtendRequested += BringShellForward;
            WebHostWindow.SetTick(250, OnTick);
        }
        catch (Exception exception)
        {
            // Сбой отсюда уронил бы киоск, а агент перезапускал бы его по кругу без следа. Пишем в журнал и держим окно.
            PlayerShellStartupLog.Write("Player Shell startup failed.", exception);
            if (window is not null)
            {
                ApplyWatchdog(new WebViewWatchdogAction(ShowFallback: true, RestartWebView: false));
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        keyboard.Dispose();
        windows.Dispose();
        overlay?.Close();
        lifetime.Cancel();
        lifetime.Dispose();
        apiHttp.Dispose();
    }

    private string? AuthorizationFor(string url)
    {
        var decision = AuthorizationHeaderPolicy.Decide(url, ApiBaseUrl(), session.AccessToken);
        return decision.ShouldInject ? decision.HeaderValue : null;
    }

    private static string JsonString(string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private void OnPageLoaded()
    {
        // Страница загрузилась — значит, движок здоров: запас повторов возвращается.
        loadRestartCount = 0;
        window?.HideFallback();
    }

    private void OnPageLoadFailed()
    {
        try
        {
            // Сбой загрузки лечится повтором; но упрямый сбой (нет файлов, битая установка) не должен превращаться в
            // шторм перезагрузок — после предела остаётся устойчивый запасной экран.
            if (loadRestartCount >= MaxLoadRestarts)
            {
                PlayerShellStartupLog.Write($"The screen failed to load; restart {loadRestartCount}/{MaxLoadRestarts}. Showing fallback.");
                ApplyWatchdog(new WebViewWatchdogAction(ShowFallback: true, RestartWebView: false));
                return;
            }

            loadRestartCount++;
            ApplyWatchdog(new WebViewWatchdogAction(ShowFallback: true, RestartWebView: true));
        }
        catch (Exception exception)
        {
            PlayerShellStartupLog.Write("Watchdog failed handling a load failure.", exception);
        }
    }

    private void ApplyWatchdog(WebViewWatchdogAction action)
    {
        if (window is null)
        {
            return;
        }

        if (action.ShowFallback)
        {
            // Перезапуск — короткая заминка; без него плашка остаётся, и гостю нужен человек, а не обещание
            // «восстанавливаем», которое не сбудется.
            window.ShowFallback(
                Shell.RemainingTimeFormatter.Format(latestState?.RemainingSeconds),
                localization.T(action.RestartWebView ? "playerShell.fallback.reconnecting" : "playerShell.fallback.broken"));
        }
        else
        {
            window.HideFallback();
        }

        if (action.RestartWebView)
        {
            _ = ReloadSoonAsync();
        }
    }

    private async Task ReloadSoonAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        window?.Reload();
    }

    private static string? ResolveDistIndexHtml() =>
        // Where the packaged build lands. The resolver prefers a dev-server URL when one is set
        // (checked first), otherwise serves this path from the content root; in production the file
        // is always present, and a failed load is covered by the watchdog/native fallback.
        Path.Combine(AppContext.BaseDirectory, "WebAssets", "index.html");

    private async Task HandleMessageAsync(string json)
    {
        try
        {
            var responseJson = await bridge.HandleAsync(json, lifetime.Token);
            window?.PostMessageAsJson(responseJson);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // Без общей страховки исключение отсюда уронило бы киоск, и агент перезапускал бы его по кругу. Пишем в
            // журнал и живём дальше.
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

    private void PostToPage(string eventType, object? payload) =>
        window?.PostMessageAsJson(ShellBridgeHost.Event(eventType, payload));

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
                        WebHostWindow.RunOnUiThread(() => clubMessage = message);
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
                WebHostWindow.RunOnUiThread(() =>
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
    private void OnTick()
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
            var shellInFront = window?.IsInFront == true;
            keyboard.SetShellInFront(shellInFront);

            // Окна приложений — только в сессии: на запертом экране чужое окно впереди не игра, и
            // экран блокировки оболочка держит сама.
            var sessionRuns = ShellWindowPolicy.SessionRuns(latestState);
            var visibleApps = sessionRuns ? NativeWindows.VisibleAppWindows() : [];
            StepBackForLaunchedApp(now, visibleApps);

            // Экран в сессии не бывает пустым: окон нет — оболочка возвращается, а страница просыпается.
            var appVisible = visibleApps.Count > 0;
            if (emptyScreen.Observe(sessionRuns, shellInFront, appVisible, now))
            {
                BringShellForward();
            }

            if (game.Observe(shellInFront || (sessionRuns && !appVisible), latestState, now) is { } gameActive)
            {
                SetPageAsleep(gameActive);
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
            // Таймер вызывается окном: сбой одного круга не должен уронить киоск.
            PlayerShellStartupLog.Write("Player Shell tick failed.", exception);
        }
    }

    /// <summary>Остаток по часам хоста: сколько прошло с прихода состояния, а не по часам платформы.</summary>
    private int? RemainingSecondsNow() =>
        latestState?.RemainingSeconds is { } remaining
            ? remaining - (int)Stopwatch.GetElapsedTime(stateReceivedAt).TotalSeconds
            : null;

    private void SetOnTop(bool value)
    {
        onTop = value;
        window?.SetTopmost(value);
    }

    /// <summary>
    /// «Поверх всех» только на запертом экране; при блокировке окно возвращается наверх. В
    /// обслуживании — полоса сверху, под ней рабочий стол техника.
    /// </summary>
    private void ApplyWindowLayer(PlayerShellStateDto state)
    {
        keyboard.SetMode(KeyboardBlockPolicy.ModeFor(state));
        windows.SetRules(state.BlockedWindows);
        if (window is null)
        {
            return;
        }

        var layout = ShellWindowPolicy.Layout(state);
        if (layout == ShellWindowLayout.Band)
        {
            if (!window.IsBandDocked)
            {
                // Полоса — не заслон: поверх окон техника она стоит как панель задач, а не как киоск.
                SetOnTop(true);
                window.DockBand((int)ShellWindowPolicy.BandHeight);
            }

            return;
        }

        if (window.IsBandDocked)
        {
            // Окно снова на весь монитор.
            window.UndockBand();
        }

        var wantOnTop = layout == ShellWindowLayout.Cover;
        if (onTop == wantOnTop)
        {
            return;
        }

        SetOnTop(wantOnTop);
        if (wantOnTop)
        {
            window.BringToFront();
        }
    }

    /// <summary>
    /// «Играть» нажато и агент запустил игру: окно игры выйдет вперёд, когда появится, а оболочка
    /// отступит. Наблюдатель помнит окна, что были до запуска, — они не новые.
    /// </summary>
    private void BeginLaunchFocus() =>
        launchFocus.Begin(DateTimeOffset.UtcNow, NativeWindows.VisibleAppWindows().Select(candidate => candidate.Handle).ToList());

    private void StepBackForLaunchedApp(DateTimeOffset now, IReadOnlyList<WindowInfo> visibleApps)
    {
        var handles = visibleApps.Select(candidate => candidate.Handle).ToList();
        if (launchFocus.Observe(now, handles) is not { } appWindow)
        {
            return;
        }

        if (!NativeWindows.BringToFront(appWindow))
        {
            // Windows не отдала передний план: оболочка сворачивается сама, и окно игры открыто. Если
            // оно закроется, страж пустого экрана вернёт оболочку.
            window?.Minimize();
        }
    }

    /// <summary>Сочетание «Мои приложения»: оболочка выходит вперёд, и страница открывает панель.</summary>
    private Task ShowAppsPanelAsync()
    {
        if (!ShellWindowPolicy.SessionRuns(latestState))
        {
            return Task.CompletedTask;
        }

        BringShellForward();
        SetPageAsleep(false);
        PostToPage(ShellBridgeEventTypeNames.AppsPanelRequested, null);
        return Task.CompletedTask;
    }

    /// <summary>«Продлить» в окне поверх игры: оболочка выходит вперёд, продление подтверждают там.</summary>
    private void BringShellForward()
    {
        // Окно выходит вперёд и остаётся «поверх всех» только на запертом экране — в сессии игра должна выйти за ним.
        SetOnTop(ShellWindowPolicy.ShouldStayOnTop(latestState));
        window?.BringToFront();
    }

    /// <summary>
    /// Игра впереди — страница засыпает (спека оболочки, §8): невидима и не рисует кадры. Вернулись — видна до того,
    /// как игрок её увидит.
    /// </summary>
    private void SetPageAsleep(bool asleep) => window?.SetPageVisible(!asleep);

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
