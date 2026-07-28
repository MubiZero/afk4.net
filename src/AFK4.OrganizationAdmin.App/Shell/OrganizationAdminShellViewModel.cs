using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.OrganizationAdmin.App.Audit;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.OrganizationAdmin.App.Diagnostics;
using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.OrganizationAdmin.App.Mvvm;
using AFK4.OrganizationAdmin.App.PilotSetup;
using AFK4.OrganizationAdmin.App.Players;
using AFK4.OrganizationAdmin.App.Pos;
using AFK4.OrganizationAdmin.App.Settings;
using AFK4.OrganizationAdmin.App.Shifts;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.OrganizationAdmin.App.Shell;

public sealed class OrganizationAdminShellViewModel : INotifyPropertyChanged
{
    private static readonly WorkspaceDefinition[] WorkspaceDefinitions =
    [
        new(OrganizationAdminWorkspaceKind.FloorMap, "Зал", OrganizationPermissionNames.ViewFloorMap),
        new(OrganizationAdminWorkspaceKind.Pos, "POS", OrganizationPermissionNames.CreatePosSale),
        new(OrganizationAdminWorkspaceKind.Players, "Игроки", OrganizationPermissionNames.ViewPlayers),
        new(OrganizationAdminWorkspaceKind.Shifts, "Смена", OrganizationPermissionNames.ViewShift),
        new(OrganizationAdminWorkspaceKind.Settings, "Операции", OrganizationPermissionNames.ViewDeviceDetail)
    ];

    private readonly RelayCommand navigateCommand;
    private readonly RelayCommand signOutCommand;
    private readonly RelayCommand clearTransientStateCommand;
    private OperatorUserContext? currentUser;
    private OrganizationAdminWorkspaceKind? selectedWorkspace;
    private string statusMessage = "Войдите, чтобы начать работу оператора.";
    private string realtimeConnectionState = "Отключено";

    public OrganizationAdminShellViewModel()
        : this(
            new SignInViewModel(new UnconfiguredOperatorAuthApiClient()),
            new FloorMapWorkspaceViewModel(new UnconfiguredOperatorFloorMapApiClient()),
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            new PosWorkspaceViewModel(new UnconfiguredOperatorPosApiClient()),
            new ShiftWorkspaceViewModel(new UnconfiguredOperatorShiftApiClient()),
            CreateDefaultSettingsWorkspace())
    {
    }

    public OrganizationAdminShellViewModel(SignInViewModel signIn, FloorMapWorkspaceViewModel floorMap)
        : this(
            signIn,
            floorMap,
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            new PosWorkspaceViewModel(new UnconfiguredOperatorPosApiClient()),
            new ShiftWorkspaceViewModel(new UnconfiguredOperatorShiftApiClient()),
            CreateDefaultSettingsWorkspace())
    {
    }

    public OrganizationAdminShellViewModel(
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

    public OrganizationAdminShellViewModel(
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

    public OrganizationAdminShellViewModel(
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
            parameter => NavigateTo((OrganizationAdminWorkspaceKind)parameter!),
            parameter => parameter is OrganizationAdminWorkspaceKind kind && IsWorkspaceAllowed(kind));
        signOutCommand = new RelayCommand(_ => SignOut(), _ => IsSignedIn);
        clearTransientStateCommand = new RelayCommand(_ => ClearTransientState(), _ => IsSignedIn);

        SignIn.SignedIn += ApplySignedInContext;
        Shifts.PropertyChanged += OnShiftsPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Organization Admin";

    public SignInViewModel SignIn { get; }

    public FloorMapWorkspaceViewModel FloorMap { get; }

    public PlayerSearchViewModel Players { get; }

    public PosWorkspaceViewModel Pos { get; }

    public ShiftWorkspaceViewModel Shifts { get; }

    public SettingsWorkspaceViewModel Settings { get; }

    public ObservableCollection<OrganizationAdminNavigationItemViewModel> NavigationItems { get; }

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

    public OrganizationAdminWorkspaceKind? SelectedWorkspace
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
        OrganizationAdminWorkspaceKind.FloorMap => "Зал",
        OrganizationAdminWorkspaceKind.Pos => "POS",
        OrganizationAdminWorkspaceKind.Players => "Игроки",
        OrganizationAdminWorkspaceKind.Shifts => "Смена",
        OrganizationAdminWorkspaceKind.Settings => "Операции",
        _ => "Оператор"
    };

    public string SelectedWorkspaceSubtitle => SelectedWorkspace switch
    {
        OrganizationAdminWorkspaceKind.FloorMap => "Запуск, завершение и контроль игровых мест.",
        OrganizationAdminWorkspaceKind.Pos => "Продажи в течение открытой смены.",
        OrganizationAdminWorkspaceKind.Players => "Поиск игроков, создание аккаунтов, кошелек и долг.",
        OrganizationAdminWorkspaceKind.Shifts => "Открытие, контроль и закрытие кассовой смены.",
        OrganizationAdminWorkspaceKind.Settings => "Настройка филиала, диагностика, аудит, устройства и обновления.",
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
                NavigationItems.Add(new OrganizationAdminNavigationItemViewModel(
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

        if (SelectedWorkspace == OrganizationAdminWorkspaceKind.FloorMap)
        {
            _ = FloorMap.LoadAsync(context.BranchId, CancellationToken.None);
        }
    }

    public void NavigateTo(OrganizationAdminWorkspaceKind workspace)
    {
        if (!IsWorkspaceAllowed(workspace))
        {
            return;
        }

        SelectedWorkspace = workspace;
    }

    public bool CanNavigateTo(OrganizationAdminWorkspaceKind workspace)
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

    private bool IsWorkspaceAllowed(OrganizationAdminWorkspaceKind workspace)
    {
        return CurrentUser is not null && IsWorkspaceAllowed(CurrentUser.Permissions, workspace);
    }

    private static bool IsWorkspaceAllowed(IReadOnlySet<string> permissions, OrganizationAdminWorkspaceKind workspace)
    {
        return workspace switch
        {
            OrganizationAdminWorkspaceKind.FloorMap => HasAny(
                permissions,
                OrganizationPermissionNames.ViewFloorMap),
            OrganizationAdminWorkspaceKind.Pos => HasAny(
                permissions,
                OrganizationPermissionNames.CreatePosSale,
                OrganizationPermissionNames.PayPosSale,
                OrganizationPermissionNames.RefundPosSale,
                OrganizationPermissionNames.VoidPosSale,
                OrganizationPermissionNames.ViewReceipt),
            OrganizationAdminWorkspaceKind.Players => HasAny(
                permissions,
                OrganizationPermissionNames.ViewPlayers,
                OrganizationPermissionNames.CreatePlayerAccount,
                OrganizationPermissionNames.ViewBilling,
                OrganizationPermissionNames.TopUpWallet,
                OrganizationPermissionNames.PayDebt,
                OrganizationPermissionNames.PurchasePackage),
            OrganizationAdminWorkspaceKind.Shifts => HasAny(
                permissions,
                OrganizationPermissionNames.ViewShift,
                OrganizationPermissionNames.OpenShift,
                OrganizationPermissionNames.CloseShift,
                OrganizationPermissionNames.ManageShiftCash,
                OrganizationPermissionNames.ViewReports),
            OrganizationAdminWorkspaceKind.Settings => HasAny(
                permissions,
                OrganizationPermissionNames.ViewDeviceDetail,
                OrganizationPermissionNames.CreateDeviceEnrollmentCode,
                OrganizationPermissionNames.DispatchDeviceCommand,
                OrganizationPermissionNames.RotateDeviceCredential,
                OrganizationPermissionNames.RevokeDeviceCredential,
                OrganizationPermissionNames.ManageInventoryStock,
                OrganizationPermissionNames.ManagePosCatalog,
                OrganizationPermissionNames.ManageTariffs,
                OrganizationPermissionNames.ManageBranchStaff,
                OrganizationPermissionNames.ManageLayout,
                OrganizationPermissionNames.AssignDeviceSeat,
                OrganizationPermissionNames.ManagePackages,
                OrganizationPermissionNames.ManageRoles,
                OrganizationPermissionNames.ViewUpdateStatus,
                OrganizationPermissionNames.ManageUpdatePackages,
                OrganizationPermissionNames.ManageUpdateRollouts,
                OrganizationPermissionNames.ViewDiagnostics,
                OrganizationPermissionNames.ViewAudit),
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
        OrganizationAdminWorkspaceKind Kind,
        string Label,
        string RequiredPermission);
}
