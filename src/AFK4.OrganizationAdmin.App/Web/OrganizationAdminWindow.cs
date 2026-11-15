using System.Text.Json;
using AFK4.Localization;
using AFK4.OrganizationAdmin.App;
using AFK4.OrganizationAdmin.App.Configuration;
using AFK4.WebHost;

namespace AFK4.OrganizationAdmin.Web;

/// <summary>Команда окна от экрана: <c>window:drag</c>, <c>window:resize</c> (с краем), <c>window:minimize</c> и так далее.</summary>
internal sealed record OrganizationAdminWindowMessage(string? Type, string? Edge = null, string? Theme = null);

/// <summary>
/// Окно Панели: экран на Miniblink (AFK4.WebHost, Native AOT — docs/roadmap/windows7.md) без рамки Windows. Заголовок и кнопки
/// рисует сам экран и шлёт команды окна; остальные сообщения — запросы к мосту (OrganizationAdminWebHostBridge).
/// Положение и размер запоминаются между запусками.
/// </summary>
internal sealed class OrganizationAdminWindow
{
    private const int DefaultWidth = 1152;
    private const int DefaultHeight = 840;
    private const int MinWidth = 960;
    private const int MinHeight = 600;

    private readonly OrganizationAdminOptions appOptions;
    private readonly OrganizationAdminWebShellOptions shellOptions;
    private readonly OrganizationAdminWebAssetResolver assetResolver;
    private readonly OrganizationAdminWebHostBridge hostBridge;
    private readonly LocalizationService localization;
    private WebHostWindow? window;
    private string? startupFailure;

    public OrganizationAdminWindow(
        OrganizationAdminOptions appOptions,
        OrganizationAdminWebShellOptions shellOptions,
        OrganizationAdminWebAssetResolver assetResolver,
        OrganizationAdminWebHostBridge hostBridge)
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
    }

    // The dev harness runs the Organization Admin UI from a localhost vite dev server (env-provisioned).
    // It's the one legitimate localhost scenario.
    private bool IsDevShell => shellOptions.DevServerUrl is not null;

    public void Show()
    {
        try
        {
            // In a packaged/normal run an unprovisioned platform URL still defaults to localhost, which would
            // silently load a dead UI. Surface a clear "not configured" failure instead. The dev harness
            // legitimately runs against localhost, so skip the guard there.
            if (!IsDevShell && appOptions.PlatformBaseUrl.IsLoopback)
            {
                startupFailure = localization.T("operator.host.notConfigured");
            }

            var launchTarget = startupFailure is null ? assetResolver.Resolve(shellOptions) : null;
            window = new WebHostWindow(new WebHostOptions
            {
                Title = "Панель AFK4.NET",
                ContentRoot = launchTarget?.LocalFolderPath ?? AppContext.BaseDirectory,
                VirtualHost = OrganizationAdminWebAssetResolver.LocalVirtualHost,
                DevServerUrl = launchTarget is { UsesLocalFolder: false } ? launchTarget.Source : null,
                InitScript = launchTarget is null ? null : OrganizationAdminWebBootstrapScript.Create(appOptions, launchTarget),
                DataFolder = OrganizationAdminDataFolder.Resolve(),
                Width = DefaultWidth,
                Height = DefaultHeight,
                MinWidth = MinWidth,
                MinHeight = MinHeight,
                Frameless = true,
                // Ссылки с экрана наружу не идут: Панель на кассе не открывает браузер и новые окна.
                OpenExternalLinks = false,
                BackgroundRgb = 0x0E1117,
                BorderRgb = 0x1B2330,
                InitialBounds = RestoredBounds(out var maximized),
                StartMaximized = maximized
            });
        }
        catch (Exception exception)
        {
            startupFailure = exception.Message;
            OrganizationAdminStartupLog.Write("Organization Admin window could not start.", exception);
            window ??= new WebHostWindow(new WebHostOptions
            {
                Title = "Панель AFK4.NET",
                ContentRoot = AppContext.BaseDirectory,
                VirtualHost = OrganizationAdminWebAssetResolver.LocalVirtualHost,
                DataFolder = OrganizationAdminDataFolder.Resolve(),
                Frameless = true,
                OpenExternalLinks = false,
                BackgroundRgb = 0x0E1117,
                BorderRgb = 0x1B2330
            });
        }

        window.MessageReceived += json => _ = HandleMessageAsync(json);
        window.Diagnostic += text => OrganizationAdminStartupLog.Write("Organization Admin window: " + text);
        window.PageLoadFailed += () => ShowStartupFailure(string.Format(localization.T("operator.host.navFailed"), "load failed"));
        window.Closing += SaveCurrentPlacement;

        if (startupFailure is not null)
        {
            window.Show(loadPage: false);
            ShowStartupFailure(startupFailure);
            return;
        }

        window.Show();
    }

    public void Close() => window?.Close();

    /// <summary>
    /// Положение, которое оператор оставил в прошлый раз, в пикселях; первый запуск — по центру рабочей области. Сохранённое
    /// лежит в точках (DIP, как у прежнего окна на WPF) и пересчитывается на масштаб экрана.
    /// </summary>
    private static (int X, int Y, int Width, int Height)? RestoredBounds(out bool maximized)
    {
        maximized = false;
        var scale = WebHostWindow.Scale;
        var saved = OrganizationAdminWindowPlacementStore.Load();
        var work = WebHostWindow.PrimaryWorkArea();
        var virtualScreen = WebHostWindow.VirtualScreen();
        var sane = saved is null
            ? null
            : (saved with
            {
                Left = saved.Left * scale,
                Top = saved.Top * scale,
                Width = saved.Width * scale,
                Height = saved.Height * scale
            }).Sanitize(virtualScreen.Left, virtualScreen.Top, virtualScreen.Width, virtualScreen.Height, MinWidth * scale, MinHeight * scale);
        if (sane is null)
        {
            var fitted = OrganizationAdminWindowPlacement.FirstRun(work.Left, work.Top, work.Width, work.Height, DefaultWidth * scale, DefaultHeight * scale);
            return ((int)fitted.Left, (int)fitted.Top, (int)fitted.Width, (int)fitted.Height);
        }

        // Обычные размеры остаются тем, во что окно вернётся; развёрнутое окно Windows ограничит рабочей областью.
        maximized = saved!.Maximized;
        return ((int)sane.Left, (int)sane.Top, (int)sane.Width, (int)sane.Height);
    }

    // Persist the geometry to restore next launch: the normal-state bounds (what the window falls back to when
    // un-maximized) plus the maximized flag.
    private void SaveCurrentPlacement()
    {
        if (window is null)
        {
            return;
        }

        var (bounds, maximized) = window.GetPlacement();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var scale = WebHostWindow.Scale;
        OrganizationAdminWindowPlacementStore.Save(new OrganizationAdminWindowPlacement
        {
            Left = bounds.X / scale,
            Top = bounds.Y / scale,
            Width = bounds.Width / scale,
            Height = bounds.Height / scale,
            Maximized = maximized,
        });
    }

    private async Task HandleMessageAsync(string json)
    {
        // async void-подобный путь: исключение отсюда уронило бы Панель. Пишем в журнал и отвечаем странице ошибкой, чтобы
        // React не ждал ответа вечно.
        try
        {
            if (TryHandleWindowMessage(json))
            {
                return;
            }

            var response = await hostBridge.HandleAsync(json, CancellationToken.None);
            if (response is not null)
            {
                window?.PostMessageAsJson(response);
            }
        }
        catch (Exception exception)
        {
            OrganizationAdminStartupLog.Write("Unhandled error in Organization Admin host bridge.", exception);
            if (TryReadRequestId(json) is { } requestId)
            {
                window?.PostMessageAsJson(OrganizationAdminWebHostBridge.CreateErrorResponse(requestId, "host_error", exception.Message));
            }
        }
    }

    private static string? TryReadRequestId(string webMessageJson)
    {
        try
        {
            using var document = JsonDocument.Parse(webMessageJson);
            return document.RootElement.TryGetProperty("requestId", out var requestId) && requestId.GetString() is { Length: > 0 } id ? id : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool TryHandleWindowMessage(string webMessageJson)
    {
        OrganizationAdminWindowMessage? message;
        try
        {
            message = JsonSerializer.Deserialize(webMessageJson, OrganizationAdminBridgeJson.Default.OrganizationAdminWindowMessage);
        }
        catch (JsonException)
        {
            // Ignore malformed web messages. The bridge is intentionally narrow.
            return true;
        }

        if (window is null)
        {
            return false;
        }

        switch (message?.Type)
        {
            case "window:drag":
                window.BeginDrag();
                return true;
            case "window:resize":
                window.BeginResize(message.Edge);
                return true;
            case "window:minimize":
                window.Minimize();
                return true;
            case "window:maximize":
                window.ToggleMaximize();
                return true;
            case "window:close":
                window.Close();
                return true;
            case "window:theme":
                // Иконка окна — из exe: смена светлой/тёмной иконки окна на WPF в новое окно не переехала.
                return true;
            default:
                return false;
        }
    }

    private void ShowStartupFailure(string message) =>
        window?.ShowStatus(localization.T("operator.host.failedTitle"), message);
}
