using System.Net.Http;
using System.Windows;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Configuration;
using AFK4.Operator.App.Devices;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Realtime;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Players;
using AFK4.Operator.App.Sessions;
using AFK4.Operator.App.Settings;
using AFK4.Operator.App.Shell;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    private readonly OperatorAppOptions options = new();
    private readonly HttpClient apiHttpClient;
    private readonly FloorMapWorkspaceViewModel floorMapViewModel;
    private readonly OperatorShellViewModel shellViewModel;
    private IOperatorRealtimeClient? realtimeClient;

    public MainWindow()
    {
        InitializeComponent();

        apiHttpClient = new HttpClient
        {
            BaseAddress = options.PlatformBaseUrl
        };
        var tokenStore = new ProtectedDataOperatorTokenStore();
        var authApiClient = new HttpOperatorAuthApiClient(apiHttpClient, tokenStore);
        var floorMapApiClient = new HttpOperatorFloorMapApiClient(apiHttpClient, tokenStore);
        var sessionApiClient = new HttpOperatorSessionApiClient(apiHttpClient, tokenStore);
        var playerApiClient = new HttpOperatorPlayerApiClient(apiHttpClient, tokenStore);
        var deviceApiClient = new HttpOperatorDeviceApiClient(apiHttpClient, tokenStore);
        var settingsViewModel = new SettingsWorkspaceViewModel(
            new HashSet<string>(),
            new TechnicianDeviceWorkflowViewModel(deviceApiClient))
        {
            ApiBaseUrlText = options.PlatformBaseUrl.ToString()
        };

        floorMapViewModel = new FloorMapWorkspaceViewModel(
            floorMapApiClient,
            sessionApiClient,
            new GuidIdempotencyKeyFactory());
        shellViewModel = new OperatorShellViewModel(
            new SignInViewModel(authApiClient),
            floorMapViewModel,
            new PlayerSearchViewModel(playerApiClient, new GuidIdempotencyKeyFactory()),
            settingsViewModel);
        DataContext = shellViewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var startup = new OperatorRealtimeStartup(
            () => new OperatorRealtimeClient(floorMapViewModel, new Uri(options.PlatformBaseUrl, "/hubs/devices")));

        realtimeClient = await startup.StartAsync(CancellationToken.None);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (realtimeClient is not null)
        {
            await realtimeClient.DisposeAsync();
        }

        apiHttpClient.Dispose();
    }
}
