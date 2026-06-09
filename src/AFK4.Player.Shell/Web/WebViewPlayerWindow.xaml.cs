using System.IO;
using System.Threading;
using System.Windows;
using AFK4.Player.Shell.Configuration;
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
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Browser.EnsureCoreWebView2Async();
        HardenForKiosk(Browser.CoreWebView2);

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

    private void OnClosed(object? sender, EventArgs e)
    {
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
