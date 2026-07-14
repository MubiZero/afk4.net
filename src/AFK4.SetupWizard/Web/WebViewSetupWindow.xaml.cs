using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AFK4.SetupWizard.Core;
using Microsoft.Web.WebView2.Core;

namespace AFK4.SetupWizard.Web;

public partial class WebViewSetupWindow : Window
{
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 2;

    private readonly SetupWizardWebShellOptions shellOptions;
    private readonly SetupWizardWebAssetResolver assetResolver;
    private readonly SetupWizardWebHostBridge hostBridge;
    private readonly SetupWizardMachineInfo machineInfo;
    private readonly Uri platformBaseUrl;
    private readonly bool isPreview;
    private bool browserInitializationStarted;

    public WebViewSetupWindow(
        SetupWizardWebShellOptions shellOptions,
        SetupWizardWebAssetResolver assetResolver,
        SetupWizardWebHostBridge hostBridge,
        SetupWizardMachineInfo machineInfo,
        Uri platformBaseUrl,
        bool isPreview)
    {
        ArgumentNullException.ThrowIfNull(shellOptions);
        ArgumentNullException.ThrowIfNull(assetResolver);
        ArgumentNullException.ThrowIfNull(hostBridge);
        ArgumentNullException.ThrowIfNull(machineInfo);
        ArgumentNullException.ThrowIfNull(platformBaseUrl);

        this.shellOptions = shellOptions;
        this.assetResolver = assetResolver;
        this.hostBridge = hostBridge;
        this.machineInfo = machineInfo;
        this.platformBaseUrl = platformBaseUrl;
        this.isPreview = isPreview;

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

            var userDataFolder = SetupWizardWebView2UserDataFolder.EnsureExists();
            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Browser.EnsureCoreWebView2Async(webViewEnvironment);
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            if (launchTarget.UsesLocalFolder)
            {
                Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    SetupWizardWebAssetResolver.LocalVirtualHost,
                    launchTarget.LocalFolderPath!,
                    CoreWebView2HostResourceAccessKind.Allow);
            }

            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                SetupWizardWebBootstrapScript.Create(launchTarget, machineInfo, platformBaseUrl, isPreview));

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
            // Сообщаем вебу стартовое состояние окна, чтобы кнопка maximize/restore сразу
            // показала верную иконку (на случай запуска в развёрнутом виде).
            PostWindowState();
            return;
        }

        ShowStartupFailure($"Не удалось загрузить веб-интерфейс: {e.WebErrorStatus}.");
    }

    // Веб не угадывает состояние окна, а получает его отсюда: при любом изменении (кнопка,
    // двойной клик по титлбару, Win+↑, snap) шлём текущее состояние, чтобы иконка/подсказка
    // кнопки развернуть/восстановить всегда совпадали с реальностью.
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        PostWindowState();
    }

    private void PostWindowState()
    {
        if (Browser.CoreWebView2 is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            type = "window:state",
            maximized = WindowState == WindowState.Maximized,
        });
        Browser.CoreWebView2.PostWebMessageAsJson(payload);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // async void: any escaping exception would crash the wizard mid-enrollment with no
        // handler, potentially leaving the device half-enrolled. HandleAsync already maps known
        // failures to structured bridge errors; this guards the unexpected.
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
            SetupWizardStartupLog.Write("Unhandled error while processing a host bridge message.", exception);
            ShowStartupFailure($"Произошла непредвиденная ошибка: {exception.Message}");
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
                    return true;
                case "window:minimize":
                    WindowState = WindowState.Minimized;
                    return true;
                case "window:toggleMaximize":
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                    return true;
                case "window:close":
                    Close();
                    return true;
                case "window:theme":
                    ApplyIconForTheme(document.RootElement.TryGetProperty("theme", out var themeProperty)
                        ? themeProperty.GetString()
                        : null);
                    return true;
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            return true;
        }
    }

    // Иконка синхронна с темой (не инвертирована): тёмная тема → тёмная иконка,
    // светлая тема → светлая иконка.
    private static readonly Uri LightIconUri = new("pack://application:,,,/Assets/afk4-icon-light.png");
    private static readonly Uri DarkIconUri = new("pack://application:,,,/Assets/afk4-icon-dark.png");

    private void ApplyIconForTheme(string? theme)
    {
        Icon = new BitmapImage(theme == "light" ? LightIconUri : DarkIconUri);
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

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void ShowStartupFailure(string message)
    {
        StatusTitle.Text = "Не удалось запустить мастер настройки";
        StatusText.Text = message;
        StartupOverlay.Visibility = Visibility.Visible;
    }
}
