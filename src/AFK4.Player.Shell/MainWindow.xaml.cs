using System.Windows;
using AFK4.Localization;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Realtime;
using AFK4.Player.Shell.Shell;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource stateClientCancellation = new();
    private readonly IPlayerShellStateClient stateClient;
    private readonly PlayerShellViewModel viewModel;

    public MainWindow(ILocalizationService localization)
    {
        InitializeComponent();
        var options = new PlayerShellOptions
        {
            PipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_PIPE_NAME") ?? "afk4-player-shell",
            CommandPipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_COMMAND_PIPE_NAME") ?? "afk4-player-shell-commands"
        };

        stateClient = new NamedPipePlayerShellStateClient(options);
        viewModel = new PlayerShellViewModel(new LauncherCommandClient(options), localization);
        viewModel.ApplyState(new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: false,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: [],
            Locale: localization.CurrentLocale));
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

#if DEBUG
    // Preview mode: no pipe connections, no real commands. Used by --preview launch flag.
    internal MainWindow(ILocalizationService localization, Preview.PreviewPlayerShell.PreviewContext preview)
    {
        InitializeComponent();

        stateClient = preview.StateClient;
        viewModel = new PlayerShellViewModel(preview.Launcher, localization);
        viewModel.ApplyState(preview.InitialState);
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }
#endif

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = ListenForStateAsync(stateClientCancellation.Token);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        stateClientCancellation.Cancel();
        stateClientCancellation.Dispose();
    }

    private async Task ListenForStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var state in stateClient.ReadStatesAsync(cancellationToken))
            {
                await Dispatcher.InvokeAsync(() => viewModel.ApplyState(state));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
