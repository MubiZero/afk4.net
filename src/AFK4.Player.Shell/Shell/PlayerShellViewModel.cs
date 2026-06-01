using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Localization;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Mvvm;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Shell;

public sealed class PlayerShellViewModel : INotifyPropertyChanged
{
    private readonly ILauncherCommandClient launcherCommandClient;
    private readonly ILocalizationService localization;
    private string state = PlayerShellStateNames.Locked;
    private string statusMessage;
    private string remainingTimeText = "--:--";
    private bool isLocked = true;
    private bool isSessionActive;
    private bool isGraceMode;
    private bool showWarning;
    private bool showLauncher;
    private string? lastCommandStatus;

    public PlayerShellViewModel(ILauncherCommandClient launcherCommandClient, ILocalizationService localization)
    {
        this.launcherCommandClient = launcherCommandClient;
        this.localization = localization;
        statusMessage = localization.T(StateMessageKey(PlayerShellStateNames.Locked));
        LaunchCommand = new RelayCommand(
            parameter => _ = LaunchAsync(parameter as LauncherAppViewModel, CancellationToken.None),
            parameter => parameter is LauncherAppViewModel { IsAvailable: true } && ShowLauncher);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string State
    {
        get => state;
        private set => SetField(ref state, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public string RemainingTimeText
    {
        get => remainingTimeText;
        private set => SetField(ref remainingTimeText, value);
    }

    public bool IsLocked
    {
        get => isLocked;
        private set => SetField(ref isLocked, value);
    }

    public bool IsSessionActive
    {
        get => isSessionActive;
        private set => SetField(ref isSessionActive, value);
    }

    public bool IsGraceMode
    {
        get => isGraceMode;
        private set => SetField(ref isGraceMode, value);
    }

    public bool ShowWarning
    {
        get => showWarning;
        private set => SetField(ref showWarning, value);
    }

    public bool ShowLauncher
    {
        get => showLauncher;
        private set => SetField(ref showLauncher, value);
    }

    public string? LastCommandStatus
    {
        get => lastCommandStatus;
        private set => SetField(ref lastCommandStatus, value);
    }

    public ObservableCollection<LauncherAppViewModel> LauncherApps { get; } = [];

    public ICommand LaunchCommand { get; }

    public void ApplyState(PlayerShellStateDto dto)
    {
        // The customer-facing shell follows the branch locale carried on the state
        // (P4: no per-walk-in switcher); re-resolves live on a config refresh.
        localization.SetLocale(dto.Locale);
        State = dto.State;
        IsLocked = string.Equals(dto.State, PlayerShellStateNames.Locked, StringComparison.Ordinal);
        IsGraceMode = dto.IsGraceMode || string.Equals(dto.State, PlayerShellStateNames.Grace, StringComparison.Ordinal);
        IsSessionActive =
            string.Equals(dto.State, PlayerShellStateNames.Active, StringComparison.Ordinal) ||
            IsGraceMode ||
            string.Equals(dto.State, PlayerShellStateNames.Ending, StringComparison.Ordinal);
        RemainingTimeText = RemainingTimeFormatter.Format(dto.RemainingSeconds);
        ShowWarning = IsGraceMode || (dto.RemainingSeconds is not null && dto.RemainingSeconds <= dto.WarningThresholdSeconds);
        ShowLauncher = IsSessionActive && dto.LauncherApps.Any(app => app.IsAvailable);

        // Render the customer status from the catalog by state — not the (English)
        // server-supplied dto.Message — so the kiosk shows the branch language (L5).
        StatusMessage = localization.T(StateMessageKey(dto.State));

        LauncherApps.Clear();
        foreach (var app in dto.LauncherApps)
        {
            LauncherApps.Add(new LauncherAppViewModel(app));
        }

        if (LaunchCommand is RelayCommand relay)
        {
            relay.RaiseCanExecuteChanged();
        }
    }

    public async Task LaunchAsync(LauncherAppViewModel? app, CancellationToken cancellationToken)
    {
        if (app is null || !app.IsAvailable || !ShowLauncher)
        {
            return;
        }

        var result = await launcherCommandClient.LaunchAsync(app.AppId, cancellationToken);
        LastCommandStatus = result.Status;
        StatusMessage = result.Message;
    }

    private static string StateMessageKey(string state)
    {
        return state switch
        {
            PlayerShellStateNames.Active => "shell.state.active",
            PlayerShellStateNames.Grace => "shell.state.grace",
            PlayerShellStateNames.Ending => "shell.state.ending",
            PlayerShellStateNames.Maintenance => "shell.state.maintenance",
            PlayerShellStateNames.Offline => "shell.state.offline",
            PlayerShellStateNames.Error => "shell.state.error",
            _ => "shell.state.locked"
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
