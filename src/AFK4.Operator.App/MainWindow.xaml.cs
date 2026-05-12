using System.Windows;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Realtime;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();
    private OperatorRealtimeClient? realtimeClient;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        realtimeClient = new OperatorRealtimeClient(viewModel, new Uri("http://localhost:5074/hubs/devices"));
        await realtimeClient.StartAsync(CancellationToken.None);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (realtimeClient is not null)
        {
            await realtimeClient.DisposeAsync();
        }
    }
}
