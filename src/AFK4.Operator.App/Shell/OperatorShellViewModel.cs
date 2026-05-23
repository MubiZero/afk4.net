using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Audit;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Diagnostics;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.PilotSetup;
using AFK4.Operator.App.Players;
using AFK4.Operator.App.Pos;
using AFK4.Operator.App.Settings;
using AFK4.Operator.App.Shifts;
using AFK4.Operator.App.Updates;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Shell;

public sealed class OperatorShellViewModel : INotifyPropertyChanged
{
    private static readonly WorkspaceDefinition[] WorkspaceDefinitions =
    [
        new(OperatorWorkspaceKind.FloorMap, "Зал", StaffPermissionNames.ViewFloorMap),
        new(OperatorWorkspaceKind.Pos, "POS", StaffPermissionNames.CreatePosSale),
        new(OperatorWorkspaceKind.Players, "Игроки", StaffPermissionNames.ViewPlayers),
        new(OperatorWorkspaceKind.Shifts, "Смена", StaffPermissionNames.ViewShift),
        new(OperatorWorkspaceKind.Settings, "Операции", StaffPermissionNames.ViewDeviceDetail)
    ];

    private readonly RelayCommand navigateCommand;
    private readonly RelayCommand signOutCommand;
    private readonly RelayCommand clearTransientStateCommand;
    private OperatorUserContext? currentUser;
    private OperatorWorkspaceKind? selectedWorkspace;
    private string statusMessage = "Войдите, чтобы начать работу оператора.";
    private string realtimeConnectionState = "Отключено";

    public OperatorShellViewModel()
        : this(
            new SignInViewModel(new UnconfiguredOperatorAuthApiClient()),
            new FloorMapWorkspaceViewModel(new UnconfiguredOperatorFloorMapApiClient()),
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            new PosWorkspaceViewModel(new UnconfiguredOperatorPosApiClient()),
            new ShiftWorkspaceViewModel(new UnconfiguredOperatorShiftApiClient()),
            CreateDefaultSettingsWorkspace())
    {
    }

    public OperatorShellViewModel(SignInViewModel signIn, FloorMapWorkspaceViewModel floorMap)
        : this(
            signIn,
            floorMap,
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            new PosWorkspaceViewModel(new UnconfiguredOperatorPosApiClient()),
            new ShiftWorkspaceViewModel(new UnconfiguredOperatorShiftApiClient()),
            CreateDefaultSettingsWorkspace())
    {
    }

    public OperatorShellViewModel(
        SignInViewModel signIn,
        FloorMapWorkspaceViewModel floorMap,
        PlayerSearchViewModel players)
        : this(
            signIn,
            floorMap,
            players,
            new PosWorkspaceViewModel(new UnconfiguredOperatorPosApiClient()),
            new ShiftWorkspaceViewModel(new UnconfiguredOperatorShiftApiClient()),
            CreateDefaultSettingsWorkspace())
    {
    }

    public OperatorShellViewModel(
        SignInViewModel signIn,
        FloorMapWorkspaceViewModel floorMap,
        PlayerSearchViewModel players,
        SettingsWorkspaceViewModel settings)
        : this(
            signIn,
            floorMap,
            players,
            new PosWorkspaceViewModel(new UnconfiguredOperatorPosApiClient()),
            new ShiftWorkspaceViewModel(new UnconfiguredOperatorShiftApiClient()),
            settings)
    {
    }

    public OperatorShellViewModel(
        SignInViewModel signIn,
        FloorMapWorkspaceViewModel floorMap,
        PlayerSearchViewModel players,
        PosWorkspaceViewModel pos,
        ShiftWorkspaceViewModel shifts,
        SettingsWorkspaceViewModel settings)
    {
        SignIn = signIn;
        FloorMap = floorMap;
        Players = players;
        Pos = pos;
        Shifts = shifts;
        Settings = settings;
        NavigationItems = [];
        navigateCommand = new RelayCommand(
            parameter => NavigateTo((OperatorWorkspaceKind)parameter!),
            parameter => parameter is OperatorWorkspaceKind kind && IsWorkspaceAllowed(kind));
        signOutCommand = new RelayCommand(_ => SignOut(), _ => IsSignedIn);
        clearTransientStateCommand = new RelayCommand(_ => ClearTransientState(), _ => IsSignedIn);

        SignIn.SignedIn += ApplySignedInContext;
        Shifts.PropertyChanged += OnShiftsPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "AFK4 Operator";

    public SignInViewModel SignIn { get; }

    public FloorMapWorkspaceViewModel FloorMap { get; }

    public PlayerSearchViewModel Players { get; }

    public PosWorkspaceViewModel Pos { get; }

    public ShiftWorkspaceViewModel Shifts { get; }

    public SettingsWorkspaceViewModel Settings { get; }

    public ObservableCollection<OperatorNavigationItemViewModel> NavigationItems { get; }

    public ICommand NavigateCommand => navigateCommand;

    public ICommand SignOutCommand => signOutCommand;

    public ICommand ClearTransientStateCommand => clearTransientStateCommand;

    public OperatorUserContext? CurrentUser
    {
        get => currentUser;
        private set
        {
            if (SetField(ref currentUser, value))
            {
                OnPropertyChanged(nameof(IsSignedIn));
                OnPropertyChanged(nameof(BranchSummary));
                signOutCommand.NotifyCanExecuteChanged();
                clearTransientStateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSignedIn => CurrentUser is not null;

    public string BranchSummary => CurrentUser is null
        ? ""
        : $"Филиал {CurrentUser.BranchId.ToString("N")[..8]}";

    public OperatorWorkspaceKind? SelectedWorkspace
    {
        get => selectedWorkspace;
        private set
        {
            if (SetField(ref selectedWorkspace, value))
            {
                RefreshNavigationSelection();
                navigateCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedWorkspaceTitle));
                OnPropertyChanged(nameof(SelectedWorkspaceSubtitle));
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public string CurrentShiftState => Shifts.CurrentShiftState ?? "Смена не открыта";

    public string CurrentShiftBadge => Shifts.CurrentShiftId is null
        ? "Нет смены"
        : Shifts.CurrentShiftState ?? "Открыта";

    public string SelectedWorkspaceTitle => SelectedWorkspace switch
    {
        OperatorWorkspaceKind.FloorMap => "Зал",
        OperatorWorkspaceKind.Pos => "POS",
        OperatorWorkspaceKind.Players => "Игроки",
        OperatorWorkspaceKind.Shifts => "Смена",
        OperatorWorkspaceKind.Settings => "Операции",
        _ => "Оператор"
    };

    public string SelectedWorkspaceSubtitle => SelectedWorkspace switch
    {
        OperatorWorkspaceKind.FloorMap => "Запуск, завершение и контроль игровых мест.",
        OperatorWorkspaceKind.Pos => "Продажи в течение открытой смены.",
        OperatorWorkspaceKind.Players => "Поиск игроков, создание аккаунтов, кошелек и долг.",
        OperatorWorkspaceKind.Shifts => "Открытие, контроль и закрытие кассовой смены.",
        OperatorWorkspaceKind.Settings => "Настройка филиала, диагностика, аудит, устройства и обновления.",
        _ => "Войдите, чтобы начать работу оператора."
    };

    public string RealtimeConnectionState
    {
        get => realtimeConnectionState;
        private set => SetField(ref realtimeConnectionState, value);
    }

    public void ApplySignedInContext(OperatorUserContext context)
    {
        CurrentUser = context;
        NavigationItems.Clear();

        foreach (var definition in WorkspaceDefinitions)
        {
            if (IsWorkspaceAllowed(context.Permissions, definition.Kind))
            {
                NavigationItems.Add(new OperatorNavigationItemViewModel(
                    definition.Kind,
                    definition.Label,
                    definition.RequiredPermission));
            }
        }

        SelectedWorkspace = NavigationItems.FirstOrDefault()?.Kind;
        StatusMessage = $"Вход выполнен: {context.DisplayName}.";
        navigateCommand.NotifyCanExecuteChanged();
        FloorMap.ApplyContext(context.OrganizationId, context.BranchId);
        Players.ApplyContext(context.OrganizationId, context.BranchId);
        Pos.ApplyContext(context.OrganizationId, context.BranchId);
        Shifts.ApplyContext(context.OrganizationId, context.BranchId);
        SyncCurrentShiftToPos();
        Settings.ApplyContext(context.OrganizationId, context.BranchId, context.Permissions);

        if (SelectedWorkspace == OperatorWorkspaceKind.FloorMap)
        {
            _ = FloorMap.LoadAsync(context.BranchId, CancellationToken.None);
        }
    }

    public void NavigateTo(OperatorWorkspaceKind workspace)
    {
        if (!IsWorkspaceAllowed(workspace))
        {
            return;
        }

        SelectedWorkspace = workspace;
    }

    public bool CanNavigateTo(OperatorWorkspaceKind workspace)
    {
        return IsWorkspaceAllowed(workspace);
    }

    public void SetRealtimeConnectionState(string state)
    {
        RealtimeConnectionState = state switch
        {
            "Connecting" => "Подключение",
            "Connected" => "Подключено",
            "Unavailable" => "Недоступно",
            "Disconnected" => "Отключено",
            _ => string.IsNullOrWhiteSpace(state) ? "Отключено" : state.Trim()
        };
    }

    public void ClearTransientState()
    {
        FloorMap.SelectedSeat = null;
        Players.SelectPlayer(null);
        StatusMessage = "Временный выбор очищен.";
    }

    public void SignOut()
    {
        CurrentUser = null;
        SelectedWorkspace = null;
        NavigationItems.Clear();
        Pos.SetCurrentShift(null);
        Settings.ApplyPermissions(new HashSet<string>());
        StatusMessage = "Вы вышли из системы.";
        navigateCommand.NotifyCanExecuteChanged();
    }

    private void OnShiftsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShiftWorkspaceViewModel.CurrentShiftId) or nameof(ShiftWorkspaceViewModel.CurrentShiftState))
        {
            SyncCurrentShiftToPos();
            OnPropertyChanged(nameof(CurrentShiftState));
            OnPropertyChanged(nameof(CurrentShiftBadge));
        }
    }

    private void SyncCurrentShiftToPos()
    {
        Pos.SetCurrentShift(Shifts.CurrentShiftId);
    }

    private void RefreshNavigationSelection()
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.Kind == SelectedWorkspace;
        }
    }

    private bool IsWorkspaceAllowed(OperatorWorkspaceKind workspace)
    {
        return CurrentUser is not null && IsWorkspaceAllowed(CurrentUser.Permissions, workspace);
    }

    private static bool IsWorkspaceAllowed(IReadOnlySet<string> permissions, OperatorWorkspaceKind workspace)
    {
        return workspace switch
        {
            OperatorWorkspaceKind.FloorMap => HasAny(
                permissions,
                StaffPermissionNames.ViewFloorMap),
            OperatorWorkspaceKind.Pos => HasAny(
                permissions,
                StaffPermissionNames.CreatePosSale,
                StaffPermissionNames.PayPosSale,
                StaffPermissionNames.RefundPosSale,
                StaffPermissionNames.VoidPosSale,
                StaffPermissionNames.ViewReceipt),
            OperatorWorkspaceKind.Players => HasAny(
                permissions,
                StaffPermissionNames.ViewPlayers,
                StaffPermissionNames.CreatePlayerAccount,
                StaffPermissionNames.ViewBilling,
                StaffPermissionNames.TopUpWallet,
                StaffPermissionNames.PayDebt,
                StaffPermissionNames.PurchasePackage),
            OperatorWorkspaceKind.Shifts => HasAny(
                permissions,
                StaffPermissionNames.ViewShift,
                StaffPermissionNames.OpenShift,
                StaffPermissionNames.CloseShift,
                StaffPermissionNames.ManageShiftCash,
                StaffPermissionNames.ViewReports),
            OperatorWorkspaceKind.Settings => HasAny(
                permissions,
                StaffPermissionNames.ViewDeviceDetail,
                StaffPermissionNames.CreateDeviceEnrollmentCode,
                StaffPermissionNames.DispatchDeviceCommand,
                StaffPermissionNames.RotateDeviceCredential,
                StaffPermissionNames.RevokeDeviceCredential,
                StaffPermissionNames.ManageInventoryStock,
                StaffPermissionNames.ManagePosCatalog,
                StaffPermissionNames.ManageTariffs,
                StaffPermissionNames.ManageBranchStaff,
                StaffPermissionNames.ManageLayout,
                StaffPermissionNames.AssignDeviceSeat,
                StaffPermissionNames.ManagePackages,
                StaffPermissionNames.ManageRoles,
                StaffPermissionNames.ViewUpdateStatus,
                StaffPermissionNames.ManageUpdatePackages,
                StaffPermissionNames.ManageUpdateRollouts,
                StaffPermissionNames.ViewDiagnostics,
                StaffPermissionNames.ViewAudit),
            _ => false
        };
    }

    private static bool HasAny(IReadOnlySet<string> permissions, params string[] requiredPermissions)
    {
        return requiredPermissions.Any(permissions.Contains);
    }

    private static SettingsWorkspaceViewModel CreateDefaultSettingsWorkspace()
    {
        return new SettingsWorkspaceViewModel(
            new HashSet<string>(),
            technicianTools: null,
            new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient()),
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()),
            new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient()),
            new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient()));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record WorkspaceDefinition(
        OperatorWorkspaceKind Kind,
        string Label,
        string RequiredPermission);
}
