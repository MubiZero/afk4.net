using System.Net.Http;
using System.Windows;
using AFK4.Operator.App.Audit;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Configuration;
using AFK4.Operator.App.Devices;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Hotkeys;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Players;
using AFK4.Operator.App.Pos;
using AFK4.Operator.App.Realtime;
using AFK4.Operator.App.Sessions;
using AFK4.Operator.App.Settings;
using AFK4.Operator.App.Shell;
using AFK4.Operator.App.Shifts;
using AFK4.Operator.App.Updates;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    private readonly OperatorAppOptions options = new();
    private readonly HttpClient apiHttpClient;
    private readonly FloorMapWorkspaceViewModel floorMapViewModel;
    private readonly PosWorkspaceViewModel posViewModel;
    private readonly ShiftWorkspaceViewModel shiftViewModel;
    private readonly OperatorShellViewModel shellViewModel;
    private readonly OperatorHotkeyService hotkeyService = new();
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
        var posApiClient = new HttpOperatorPosApiClient(apiHttpClient, tokenStore);
        var shiftApiClient = new HttpOperatorShiftApiClient(apiHttpClient, tokenStore);
        var deviceApiClient = new HttpOperatorDeviceApiClient(apiHttpClient, tokenStore);
        var updateApiClient = new HttpOperatorUpdateApiClient(apiHttpClient, tokenStore);
        var auditApiClient = new HttpOperatorAuditApiClient(apiHttpClient, tokenStore);
        var settingsViewModel = new SettingsWorkspaceViewModel(
            new HashSet<string>(),
            new TechnicianDeviceWorkflowViewModel(deviceApiClient),
            new UpdateStatusWorkspaceViewModel(updateApiClient),
            new AuditSearchWorkspaceViewModel(auditApiClient))
        {
            ApiBaseUrlText = options.PlatformBaseUrl.ToString()
        };

        floorMapViewModel = new FloorMapWorkspaceViewModel(
            floorMapApiClient,
            sessionApiClient,
            new GuidIdempotencyKeyFactory());
        posViewModel = new PosWorkspaceViewModel(posApiClient, new GuidIdempotencyKeyFactory());
        shiftViewModel = new ShiftWorkspaceViewModel(
            shiftApiClient,
            new GuidIdempotencyKeyFactory(),
            new SaveFileReportCsvFileWriter());
        shellViewModel = new OperatorShellViewModel(
            new SignInViewModel(authApiClient),
            floorMapViewModel,
            new PlayerSearchViewModel(playerApiClient, new GuidIdempotencyKeyFactory()),
            posViewModel,
            shiftViewModel,
            settingsViewModel);
        DataContext = shellViewModel;

        RegisterHotkeys();
        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        shellViewModel.SetRealtimeConnectionState("Connecting");
        var startup = new OperatorRealtimeStartup(
            () => new OperatorRealtimeClient(floorMapViewModel, new Uri(options.PlatformBaseUrl, "/hubs/devices")));

        realtimeClient = await startup.StartAsync(CancellationToken.None);
        shellViewModel.SetRealtimeConnectionState(realtimeClient is null ? "Unavailable" : "Connected");
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        PreviewKeyDown -= OnPreviewKeyDown;

        if (realtimeClient is not null)
        {
            await realtimeClient.DisposeAsync();
        }

        apiHttpClient.Dispose();
    }

    private void RegisterHotkeys()
    {
        hotkeyService.RegisterGlobal(
            "F1",
            NavigateCommand(OperatorWorkspaceKind.FloorMap));
        hotkeyService.Register(
            OperatorWorkspaceKind.FloorMap,
            "F2",
            floorMapViewModel.SeatContext.StartGuestSessionCommand);
        hotkeyService.Register(
            OperatorWorkspaceKind.FloorMap,
            "F3",
            floorMapViewModel.SeatContext.ExtendSessionCommand);
        hotkeyService.Register(
            OperatorWorkspaceKind.FloorMap,
            "F4",
            floorMapViewModel.SeatContext.EndSessionCommand);
        hotkeyService.Register(
            OperatorWorkspaceKind.FloorMap,
            "F5",
            floorMapViewModel.RefreshCommand);
        hotkeyService.Register(
            OperatorWorkspaceKind.Players,
            "F5",
            shellViewModel.Players.SearchCommand);
        hotkeyService.Register(
            OperatorWorkspaceKind.Pos,
            "F5",
            posViewModel.RefreshCatalogCommand);
        hotkeyService.Register(
            OperatorWorkspaceKind.Shifts,
            "F5",
            shiftViewModel.LoadCurrentShiftCommand);
        hotkeyService.RegisterGlobal(
            "F6",
            NavigateCommand(OperatorWorkspaceKind.Pos));
        hotkeyService.RegisterGlobal(
            "F7",
            NavigateCommand(OperatorWorkspaceKind.Players));
        hotkeyService.RegisterGlobal(
            "F8",
            new RelayCommand(
                _ =>
                {
                    shellViewModel.NavigateTo(OperatorWorkspaceKind.Shifts);
                    if (shiftViewModel.LoadCurrentShiftCommand.CanExecute(null))
                    {
                        shiftViewModel.LoadCurrentShiftCommand.Execute(null);
                    }
                },
                _ => shellViewModel.CanNavigateTo(OperatorWorkspaceKind.Shifts)));
        hotkeyService.RegisterGlobal("Ctrl+L", shellViewModel.SignOutCommand);
        hotkeyService.RegisterGlobal("Esc", shellViewModel.ClearTransientStateCommand);
    }

    private RelayCommand NavigateCommand(OperatorWorkspaceKind workspace)
    {
        return new RelayCommand(
            _ => shellViewModel.NavigateTo(workspace),
            _ => shellViewModel.CanNavigateTo(workspace));
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!shellViewModel.IsSignedIn)
        {
            return;
        }

        var gesture = CreateGesture(e);
        if (gesture is null)
        {
            return;
        }

        var workspace = shellViewModel.SelectedWorkspace ?? OperatorWorkspaceKind.FloorMap;
        if (hotkeyService.TryExecute(workspace, gesture))
        {
            e.Handled = true;
        }
    }

    private static string? CreateGesture(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key is
            System.Windows.Input.Key.LeftCtrl or
            System.Windows.Input.Key.RightCtrl or
            System.Windows.Input.Key.LeftAlt or
            System.Windows.Input.Key.RightAlt or
            System.Windows.Input.Key.LeftShift or
            System.Windows.Input.Key.RightShift)
        {
            return null;
        }

        var parts = new List<string>();
        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        if ((modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt)
        {
            parts.Add("Alt");
        }

        if ((modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(key == System.Windows.Input.Key.Escape ? "Esc" : key.ToString());
        return string.Join("+", parts);
    }
}
