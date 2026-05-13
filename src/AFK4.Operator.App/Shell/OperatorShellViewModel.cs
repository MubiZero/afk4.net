using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Players;
using AFK4.Operator.App.Settings;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Shell;

public sealed class OperatorShellViewModel : INotifyPropertyChanged
{
    private static readonly WorkspaceDefinition[] WorkspaceDefinitions =
    [
        new(OperatorWorkspaceKind.FloorMap, "Floor", StaffPermissionNames.ViewFloorMap),
        new(OperatorWorkspaceKind.Pos, "POS", StaffPermissionNames.CreatePosSale),
        new(OperatorWorkspaceKind.Players, "Players", StaffPermissionNames.ViewPlayers),
        new(OperatorWorkspaceKind.Shifts, "Shifts", StaffPermissionNames.ViewShift),
        new(OperatorWorkspaceKind.Settings, "Settings", StaffPermissionNames.ViewDeviceDetail)
    ];

    private readonly RelayCommand navigateCommand;
    private readonly RelayCommand signOutCommand;
    private OperatorUserContext? currentUser;
    private OperatorWorkspaceKind? selectedWorkspace;
    private string statusMessage = "Sign in to start operator work.";

    public OperatorShellViewModel()
        : this(
            new SignInViewModel(new UnconfiguredOperatorAuthApiClient()),
            new FloorMapWorkspaceViewModel(new UnconfiguredOperatorFloorMapApiClient()),
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            new SettingsWorkspaceViewModel(new HashSet<string>()))
    {
    }

    public OperatorShellViewModel(SignInViewModel signIn, FloorMapWorkspaceViewModel floorMap)
        : this(
            signIn,
            floorMap,
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            new SettingsWorkspaceViewModel(new HashSet<string>()))
    {
    }

    public OperatorShellViewModel(
        SignInViewModel signIn,
        FloorMapWorkspaceViewModel floorMap,
        PlayerSearchViewModel players)
        : this(signIn, floorMap, players, new SettingsWorkspaceViewModel(new HashSet<string>()))
    {
    }

    public OperatorShellViewModel(
        SignInViewModel signIn,
        FloorMapWorkspaceViewModel floorMap,
        PlayerSearchViewModel players,
        SettingsWorkspaceViewModel settings)
    {
        SignIn = signIn;
        FloorMap = floorMap;
        Players = players;
        Settings = settings;
        NavigationItems = [];
        navigateCommand = new RelayCommand(
            parameter => NavigateTo((OperatorWorkspaceKind)parameter!),
            parameter => parameter is OperatorWorkspaceKind kind && IsWorkspaceAllowed(kind));
        signOutCommand = new RelayCommand(_ => SignOut(), _ => IsSignedIn);

        SignIn.SignedIn += ApplySignedInContext;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "AFK4 Operator";

    public SignInViewModel SignIn { get; }

    public FloorMapWorkspaceViewModel FloorMap { get; }

    public PlayerSearchViewModel Players { get; }

    public SettingsWorkspaceViewModel Settings { get; }

    public ObservableCollection<OperatorNavigationItemViewModel> NavigationItems { get; }

    public ICommand NavigateCommand => navigateCommand;

    public ICommand SignOutCommand => signOutCommand;

    public OperatorUserContext? CurrentUser
    {
        get => currentUser;
        private set
        {
            if (SetField(ref currentUser, value))
            {
                OnPropertyChanged(nameof(IsSignedIn));
                signOutCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSignedIn => CurrentUser is not null;

    public OperatorWorkspaceKind? SelectedWorkspace
    {
        get => selectedWorkspace;
        private set
        {
            if (SetField(ref selectedWorkspace, value))
            {
                navigateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
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
        StatusMessage = $"Signed in as {context.DisplayName}.";
        navigateCommand.NotifyCanExecuteChanged();
        FloorMap.ApplyContext(context.OrganizationId, context.BranchId);
        Players.ApplyContext(context.OrganizationId, context.BranchId);
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

    public void SignOut()
    {
        CurrentUser = null;
        SelectedWorkspace = null;
        NavigationItems.Clear();
        Settings.ApplyPermissions(new HashSet<string>());
        StatusMessage = "Signed out.";
        navigateCommand.NotifyCanExecuteChanged();
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
                StaffPermissionNames.ManageShiftCash),
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
                StaffPermissionNames.ManagePackages,
                StaffPermissionNames.ManageRoles,
                StaffPermissionNames.ViewAudit),
            _ => false
        };
    }

    private static bool HasAny(IReadOnlySet<string> permissions, params string[] requiredPermissions)
    {
        return requiredPermissions.Any(permissions.Contains);
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
