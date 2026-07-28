using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.OrganizationAdmin.App.Audit;
using AFK4.OrganizationAdmin.App.Diagnostics;
using AFK4.OrganizationAdmin.App.Devices;
using AFK4.OrganizationAdmin.App.Mvvm;
using AFK4.OrganizationAdmin.App.PilotSetup;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.OrganizationAdmin.App.Settings;

public sealed class SettingsWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly TechnicianDeviceWorkflowViewModel? technicianTools;
    private readonly UpdateStatusWorkspaceViewModel? updateStatus;
    private readonly AuditSearchWorkspaceViewModel? auditSearch;
    private readonly DiagnosticsWorkspaceViewModel? diagnostics;
    private readonly PilotSetupWorkspaceViewModel? pilotSetup;
    private readonly RelayCommand selectPanelCommand;
    private string apiBaseUrlText = "http://localhost:5074";
    private string organizationIdText = string.Empty;
    private string branchIdText = string.Empty;
    private SettingsPanelViewModel? selectedPanel;

    public SettingsWorkspaceViewModel(IReadOnlySet<string> permissions)
        : this(
            permissions,
            technicianTools: null,
            new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient()),
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()),
            new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient()),
            new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools)
        : this(
            permissions,
            technicianTools,
            new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient()),
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()),
            new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient()),
            new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools,
        UpdateStatusWorkspaceViewModel? updateStatus)
        : this(
            permissions,
            technicianTools,
            updateStatus,
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()),
            new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient()),
            new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools,
        UpdateStatusWorkspaceViewModel? updateStatus,
        AuditSearchWorkspaceViewModel? auditSearch)
        : this(
            permissions,
            technicianTools,
            updateStatus,
            auditSearch,
            new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient()),
            new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools,
        UpdateStatusWorkspaceViewModel? updateStatus,
        AuditSearchWorkspaceViewModel? auditSearch,
        DiagnosticsWorkspaceViewModel? diagnostics)
        : this(
            permissions,
            technicianTools,
            updateStatus,
            auditSearch,
            diagnostics,
            new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools,
        UpdateStatusWorkspaceViewModel? updateStatus,
        AuditSearchWorkspaceViewModel? auditSearch,
        DiagnosticsWorkspaceViewModel? diagnostics,
        PilotSetupWorkspaceViewModel? pilotSetup)
    {
        this.technicianTools = technicianTools;
        this.updateStatus = updateStatus;
        this.auditSearch = auditSearch;
        this.diagnostics = diagnostics;
        this.pilotSetup = pilotSetup;
        Panels = [];
        pilotSetup?.ApplyPermissions(permissions);
        selectPanelCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is SettingsPanelViewModel panel)
                {
                    SelectedPanel = panel;
                }
            },
            parameter => parameter is SettingsPanelViewModel);

        RebuildPanels(permissions);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SettingsPanelViewModel> Panels { get; }

    public TechnicianDeviceWorkflowViewModel? TechnicianTools => technicianTools;

    public UpdateStatusWorkspaceViewModel? UpdateStatus => updateStatus;

    public AuditSearchWorkspaceViewModel? AuditSearch => auditSearch;

    public DiagnosticsWorkspaceViewModel? Diagnostics => diagnostics;

    public PilotSetupWorkspaceViewModel? PilotSetup => pilotSetup;

    public bool HasTechnicianTools => technicianTools is not null && Panels.Any(panel => panel.Key == "devices");

    public bool HasUpdateStatus => updateStatus is not null && Panels.Any(panel => panel.Key == "updates");

    public bool HasAuditSearch => auditSearch is not null && Panels.Any(panel => panel.Key == "audit");

    public bool HasDiagnostics => diagnostics is not null && Panels.Any(panel => panel.Key == "diagnostics");

    public bool HasPilotSetup => pilotSetup is not null && Panels.Any(panel => panel.Key == "pilot-setup");

    public string ApiBaseUrlText
    {
        get => apiBaseUrlText;
        set => SetField(ref apiBaseUrlText, value);
    }

    public string OrganizationIdText
    {
        get => organizationIdText;
        set => SetField(ref organizationIdText, value);
    }

    public string BranchIdText
    {
        get => branchIdText;
        set => SetField(ref branchIdText, value);
    }

    public SettingsPanelViewModel? SelectedPanel
    {
        get => selectedPanel;
        set
        {
            if (SetField(ref selectedPanel, value))
            {
                OnPropertyChanged(nameof(SelectedPanelTitle));
                OnPropertyChanged(nameof(SelectedPanelDescription));
                OnPropertyChanged(nameof(IsAdvancedPanelSelected));
            }
        }
    }

    public string SelectedPanelTitle => SelectedPanel?.Label ?? "Операции";

    public string SelectedPanelDescription => SelectedPanel?.Description ?? "Инструменты филиала и операционная настройка.";

    public bool IsAdvancedPanelSelected => SelectedPanel?.Key is "updates" or "devices" or "audit" or "diagnostics";

    public ICommand SelectPanelCommand => selectPanelCommand;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        OrganizationIdText = organizationId.ToString("D");
        BranchIdText = branchId.ToString("D");
        technicianTools?.ApplyContext(organizationId, branchId);
        updateStatus?.ApplyContext(organizationId, branchId);
        auditSearch?.ApplyContext(organizationId, branchId);
        diagnostics?.ApplyContext(organizationId, branchId);
        pilotSetup?.ApplyContext(organizationId, branchId);
    }

    public void ApplyContext(Guid organizationId, Guid branchId, IReadOnlySet<string> permissions)
    {
        RebuildPanels(permissions);
        pilotSetup?.ApplyPermissions(permissions);
        ApplyContext(organizationId, branchId);
    }

    public void ApplyPermissions(IReadOnlySet<string> permissions)
    {
        pilotSetup?.ApplyPermissions(permissions);
        RebuildPanels(permissions);
    }

    private void RebuildPanels(IReadOnlySet<string> permissions)
    {
        var selectedKey = SelectedPanel?.Key;
        Panels.Clear();

        AddPanel("connection", "Подключение", "Приложение оператора");

        if (HasAny(
            permissions,
            OrganizationPermissionNames.ViewDeviceDetail,
            OrganizationPermissionNames.CreateDeviceEnrollmentCode,
            OrganizationPermissionNames.DispatchDeviceCommand,
            OrganizationPermissionNames.ViewDeviceCommandStatus,
            OrganizationPermissionNames.RotateDeviceCredential,
            OrganizationPermissionNames.RevokeDeviceCredential))
        {
            AddPanel("devices", "Устройства", "Инструменты техника");
        }

        if (HasAny(
            permissions,
            OrganizationPermissionNames.ManagePosCatalog,
            OrganizationPermissionNames.ManageInventoryStock,
            OrganizationPermissionNames.ViewInventory))
        {
            AddPanel("pos-catalog", "Каталог POS", "Товары и остатки");
        }

        if (HasAny(
            permissions,
            OrganizationPermissionNames.ManageTariffs,
            OrganizationPermissionNames.ViewTariffs))
        {
            AddPanel("tariffs", "Тарифы", "Правила цен");
        }

        if (HasAny(
            permissions,
            OrganizationPermissionNames.ManagePackages,
            OrganizationPermissionNames.ViewPackages))
        {
            AddPanel("packages", "Пакеты", "Пакеты времени");
        }

        if (permissions.Contains(OrganizationPermissionNames.ManageRoles))
        {
            AddPanel("roles", "Роли", "Доступ персонала");
        }

        if (HasAny(
            permissions,
            OrganizationPermissionNames.ViewUpdateStatus,
            OrganizationPermissionNames.ManageUpdatePackages,
            OrganizationPermissionNames.ManageUpdateRollouts))
        {
            AddPanel("updates", "Обновления", "Пакеты и раскатки");
        }

        if (permissions.Contains(OrganizationPermissionNames.ViewAudit))
        {
            AddPanel("audit", "Аудит", "Действия операторов");
        }

        if (permissions.Contains(OrganizationPermissionNames.ViewDiagnostics))
        {
            AddPanel("diagnostics", "Диагностика", "Состояние филиала");
        }

        if (HasAny(
            permissions,
            OrganizationPermissionNames.ManageBranchStaff,
            OrganizationPermissionNames.ManageLayout,
            OrganizationPermissionNames.ManageTariffs,
            OrganizationPermissionNames.ManagePosCatalog,
            OrganizationPermissionNames.AssignDeviceSeat))
        {
            AddPanel("pilot-setup", "Пилотная настройка", "Первичная настройка филиала");
        }

        SelectedPanel = Panels.FirstOrDefault(panel => panel.Key == selectedKey) ?? Panels.FirstOrDefault();
        OnPropertyChanged(nameof(HasTechnicianTools));
        OnPropertyChanged(nameof(HasUpdateStatus));
        OnPropertyChanged(nameof(HasAuditSearch));
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasPilotSetup));
    }

    private void AddPanel(string key, string label, string description)
    {
        Panels.Add(new SettingsPanelViewModel(key, label, description));
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
}

public sealed record SettingsPanelViewModel(
    string Key,
    string Label,
    string Description);
