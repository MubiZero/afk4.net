using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Audit;
using AFK4.Operator.App.Devices;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Updates;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Settings;

public sealed class SettingsWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly TechnicianDeviceWorkflowViewModel? technicianTools;
    private readonly UpdateStatusWorkspaceViewModel? updateStatus;
    private readonly AuditSearchWorkspaceViewModel? auditSearch;
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
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools)
        : this(
            permissions,
            technicianTools,
            new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient()),
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()))
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
            new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient()))
    {
    }

    public SettingsWorkspaceViewModel(
        IReadOnlySet<string> permissions,
        TechnicianDeviceWorkflowViewModel? technicianTools,
        UpdateStatusWorkspaceViewModel? updateStatus,
        AuditSearchWorkspaceViewModel? auditSearch)
    {
        this.technicianTools = technicianTools;
        this.updateStatus = updateStatus;
        this.auditSearch = auditSearch;
        Panels = [];
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

    public bool HasTechnicianTools => technicianTools is not null && Panels.Any(panel => panel.Key == "devices");

    public bool HasUpdateStatus => updateStatus is not null && Panels.Any(panel => panel.Key == "updates");

    public bool HasAuditSearch => auditSearch is not null && Panels.Any(panel => panel.Key == "audit");

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
        set => SetField(ref selectedPanel, value);
    }

    public ICommand SelectPanelCommand => selectPanelCommand;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        OrganizationIdText = organizationId.ToString("D");
        BranchIdText = branchId.ToString("D");
        technicianTools?.ApplyContext(organizationId, branchId);
        updateStatus?.ApplyContext(organizationId, branchId);
        auditSearch?.ApplyContext(organizationId, branchId);
    }

    public void ApplyContext(Guid organizationId, Guid branchId, IReadOnlySet<string> permissions)
    {
        RebuildPanels(permissions);
        ApplyContext(organizationId, branchId);
    }

    public void ApplyPermissions(IReadOnlySet<string> permissions)
    {
        RebuildPanels(permissions);
    }

    private void RebuildPanels(IReadOnlySet<string> permissions)
    {
        var selectedKey = SelectedPanel?.Key;
        Panels.Clear();

        AddPanel("connection", "Connection", "Operator App");

        if (HasAny(
            permissions,
            StaffPermissionNames.ViewDeviceDetail,
            StaffPermissionNames.CreateDeviceEnrollmentCode,
            StaffPermissionNames.DispatchDeviceCommand,
            StaffPermissionNames.ViewDeviceCommandStatus,
            StaffPermissionNames.RotateDeviceCredential,
            StaffPermissionNames.RevokeDeviceCredential))
        {
            AddPanel("devices", "Devices", "Technician tools");
        }

        if (HasAny(
            permissions,
            StaffPermissionNames.ManagePosCatalog,
            StaffPermissionNames.ManageInventoryStock,
            StaffPermissionNames.ViewInventory))
        {
            AddPanel("pos-catalog", "POS Catalog", "Products and stock");
        }

        if (HasAny(
            permissions,
            StaffPermissionNames.ManageTariffs,
            StaffPermissionNames.ViewTariffs))
        {
            AddPanel("tariffs", "Tariffs", "Pricing rules");
        }

        if (HasAny(
            permissions,
            StaffPermissionNames.ManagePackages,
            StaffPermissionNames.ViewPackages))
        {
            AddPanel("packages", "Packages", "Time bundles");
        }

        if (permissions.Contains(StaffPermissionNames.ManageRoles))
        {
            AddPanel("roles", "Roles", "Staff access");
        }

        if (permissions.Contains(StaffPermissionNames.ViewUpdateStatus))
        {
            AddPanel("updates", "Updates", "Rollout status");
        }

        if (permissions.Contains(StaffPermissionNames.ViewAudit))
        {
            AddPanel("audit", "Audit", "Operator actions");
        }

        SelectedPanel = Panels.FirstOrDefault(panel => panel.Key == selectedKey) ?? Panels.FirstOrDefault();
        OnPropertyChanged(nameof(HasTechnicianTools));
        OnPropertyChanged(nameof(HasUpdateStatus));
        OnPropertyChanged(nameof(HasAuditSearch));
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
