using System.Net.Http;
using System.Windows;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Configuration;
using AFK4.Operator.App.Devices;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Realtime;
using AFK4.Operator.App.Shell;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    private readonly OperatorAppOptions options = new();
    private readonly HttpClient apiHttpClient;
    private readonly MainWindowViewModel floorMapViewModel;
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
        var deviceApiClient = new HttpOperatorDeviceApiClient(apiHttpClient, tokenStore);
        var authApiClient = new HttpOperatorAuthApiClient(apiHttpClient, tokenStore);

        floorMapViewModel = new MainWindowViewModel(deviceApiClient);
        shellViewModel = new OperatorShellViewModel(
            new SignInViewModel(authApiClient),
            floorMapViewModel);
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
