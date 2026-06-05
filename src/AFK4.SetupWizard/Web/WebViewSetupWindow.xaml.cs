using System.Windows;
using AFK4.SetupWizard.Core;
using Microsoft.Web.WebView2.Core;

namespace AFK4.SetupWizard.Web;

public partial class WebViewSetupWindow : Window
{
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
            return;
        }

        ShowStartupFailure($"Не удалось загрузить веб-интерфейс: {e.WebErrorStatus}.");
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var responseJson = await hostBridge.HandleAsync(e.WebMessageAsJson, CancellationToken.None);
        if (responseJson is not null && Browser.CoreWebView2 is not null)
        {
            Browser.CoreWebView2.PostWebMessageAsJson(responseJson);
        }
    }

    private void ShowStartupFailure(string message)
    {
        StatusTitle.Text = "Не удалось запустить мастер настройки";
        StatusText.Text = message;
        StartupOverlay.Visibility = Visibility.Visible;
    }
}
