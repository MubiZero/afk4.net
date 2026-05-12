using System.Windows;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Realtime;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();
    private IOperatorRealtimeClient? realtimeClient;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var startup = new OperatorRealtimeStartup(
            () => new OperatorRealtimeClient(viewModel, new Uri("http://localhost:5074/hubs/devices")));

        realtimeClient = await startup.StartAsync(CancellationToken.None);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (realtimeClient is not null)
        {
            await realtimeClient.DisposeAsync();
        }
    }
}
