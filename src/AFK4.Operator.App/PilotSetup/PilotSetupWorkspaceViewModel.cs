using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.PilotSetup;

public sealed class PilotSetupWorkspaceViewModel : INotifyPropertyChanged
{
    private string organizationIdText = string.Empty;
    private string branchIdText = string.Empty;
    private string zoneName = "Main Hall";
    private string seatPrefix = "PC-";
    private int seatCount = 10;
    private int seatSortOrderStart = 1;
    private string targetAssignmentSeatName = "PC-001";
    private string tariffName = "Standard";
    private string currencyCode = "TJS";
    private long pricePerMinuteMinorUnits = 100;
    private int minimumBillableMinutes = 1;
    private int roundingIncrementMinutes = 1;
    private DateTimeOffset effectiveFromUtc = new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
    private string productCategoryName = "Drinks";
    private string productName = "Water 0.5";
    private string productSku = "WATER-05";
    private long productPriceMinorUnits = 500;
    private bool productTrackStock = true;
    private bool productAllowNegativeStock;
    private string deviceIdText = string.Empty;
    private bool canSetupStaff;
    private bool canSetupLayout;
    private bool canSetupTariff;
    private bool canSetupPos;
    private bool canAssignDeviceSeat;
    private bool hasAnySetupPermission;

    public PilotSetupWorkspaceViewModel(IOperatorPilotSetupApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        StaffUsers =
        [
            new PilotSetupStaffUserViewModel(
                "cashier.pilot@afk4.test",
                "Pilot Cashier",
                "ChangeMe!2026",
                "cashier_operator"),
            new PilotSetupStaffUserViewModel(
                "technician.pilot@afk4.test",
                "Pilot Technician",
                "ChangeMe!2026",
                "technician"),
            new PilotSetupStaffUserViewModel(
                "supervisor.pilot@afk4.test",
                "Pilot Supervisor",
                "ChangeMe!2026",
                "shift_supervisor")
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PilotSetupStaffUserViewModel> StaffUsers { get; }

    public ObservableCollection<PilotSetupStepResultViewModel> Results { get; } = [];

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

    public string ZoneName
    {
        get => zoneName;
        set => SetField(ref zoneName, value);
    }

    public string SeatPrefix
    {
        get => seatPrefix;
        set => SetField(ref seatPrefix, value);
    }

    public int SeatCount
    {
        get => seatCount;
        set => SetField(ref seatCount, value);
    }

    public int SeatSortOrderStart
    {
        get => seatSortOrderStart;
        set => SetField(ref seatSortOrderStart, value);
    }

    public string TargetAssignmentSeatName
    {
        get => targetAssignmentSeatName;
        set => SetField(ref targetAssignmentSeatName, value);
    }

    public string TariffName
    {
        get => tariffName;
        set => SetField(ref tariffName, value);
    }

    public string CurrencyCode
    {
        get => currencyCode;
        set => SetField(ref currencyCode, value);
    }

    public long PricePerMinuteMinorUnits
    {
        get => pricePerMinuteMinorUnits;
        set => SetField(ref pricePerMinuteMinorUnits, value);
    }

    public int MinimumBillableMinutes
    {
        get => minimumBillableMinutes;
        set => SetField(ref minimumBillableMinutes, value);
    }

    public int RoundingIncrementMinutes
    {
        get => roundingIncrementMinutes;
        set => SetField(ref roundingIncrementMinutes, value);
    }

    public DateTimeOffset EffectiveFromUtc
    {
        get => effectiveFromUtc;
        set => SetField(ref effectiveFromUtc, value);
    }

    public string ProductCategoryName
    {
        get => productCategoryName;
        set => SetField(ref productCategoryName, value);
    }

    public string ProductName
    {
        get => productName;
        set => SetField(ref productName, value);
    }

    public string ProductSku
    {
        get => productSku;
        set => SetField(ref productSku, value);
    }

    public long ProductPriceMinorUnits
    {
        get => productPriceMinorUnits;
        set => SetField(ref productPriceMinorUnits, value);
    }

    public bool ProductTrackStock
    {
        get => productTrackStock;
        set => SetField(ref productTrackStock, value);
    }

    public bool ProductAllowNegativeStock
    {
        get => productAllowNegativeStock;
        set => SetField(ref productAllowNegativeStock, value);
    }

    public string DeviceIdText
    {
        get => deviceIdText;
        set => SetField(ref deviceIdText, value);
    }

    public bool CanSetupStaff
    {
        get => canSetupStaff;
        private set => SetField(ref canSetupStaff, value);
    }

    public bool CanSetupLayout
    {
        get => canSetupLayout;
        private set => SetField(ref canSetupLayout, value);
    }

    public bool CanSetupTariff
    {
        get => canSetupTariff;
        private set => SetField(ref canSetupTariff, value);
    }

    public bool CanSetupPos
    {
        get => canSetupPos;
        private set => SetField(ref canSetupPos, value);
    }

    public bool CanAssignDeviceSeat
    {
        get => canAssignDeviceSeat;
        private set => SetField(ref canAssignDeviceSeat, value);
    }

    public bool HasAnySetupPermission
    {
        get => hasAnySetupPermission;
        private set => SetField(ref hasAnySetupPermission, value);
    }

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        OrganizationIdText = organizationId.ToString("D");
        BranchIdText = branchId.ToString("D");
    }

    public void ApplyPermissions(IReadOnlySet<string> permissions)
    {
        CanSetupStaff = permissions.Contains(StaffPermissionNames.ManageBranchStaff);
        CanSetupLayout = permissions.Contains(StaffPermissionNames.ManageLayout);
        CanSetupTariff = permissions.Contains(StaffPermissionNames.ManageTariffs);
        CanSetupPos = permissions.Contains(StaffPermissionNames.ManagePosCatalog);
        CanAssignDeviceSeat = permissions.Contains(StaffPermissionNames.AssignDeviceSeat);
        HasAnySetupPermission = CanSetupStaff
            || CanSetupLayout
            || CanSetupTariff
            || CanSetupPos
            || CanAssignDeviceSeat;
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

public sealed class PilotSetupStaffUserViewModel : INotifyPropertyChanged
{
    private string userName;
    private string displayName;
    private string password;
    private string roleName;

    public PilotSetupStaffUserViewModel(string userName, string displayName, string password, string roleName)
    {
        this.userName = userName;
        this.displayName = displayName;
        this.password = password;
        this.roleName = roleName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string UserName
    {
        get => userName;
        set => SetField(ref userName, value);
    }

    public string DisplayName
    {
        get => displayName;
        set => SetField(ref displayName, value);
    }

    public string Password
    {
        get => password;
        set => SetField(ref password, value);
    }

    public string RoleName
    {
        get => roleName;
        set => SetField(ref roleName, value);
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

public sealed record PilotSetupStepResultViewModel(
    string Key,
    string Label,
    string State,
    string Detail,
    string? EntityId);
