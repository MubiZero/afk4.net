using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.PilotSetup;

public sealed class PilotSetupWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IOperatorPilotSetupApiClient apiClient;
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
    private bool isBusy;
    private string statusMessage = "Pilot setup ready.";
    private string? errorMessage;
    private IReadOnlyList<string> passwordRedactionValues = [];

    public PilotSetupWorkspaceViewModel(IOperatorPilotSetupApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, CanRunCommand);

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

    public AsyncRelayCommand ApplyCommand { get; }

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

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                ApplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
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
        ApplyCommand.NotifyCanExecuteChanged();
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        Results.Clear();
        passwordRedactionValues = CreatePasswordRedactionValues();
        ErrorMessage = null;
        StatusMessage = "Applying pilot setup...";

        if (!TryValidate(out var organizationId, out var branchId))
        {
            StatusMessage = "Pilot setup validation failed.";
            return;
        }

        IsBusy = true;

        var seatsByName = new Dictionary<string, SeatDto>(StringComparer.OrdinalIgnoreCase);
        string currentKey = "pilot-setup";
        string currentLabel = "Pilot setup";

        try
        {
            if (CanSetupStaff)
            {
                currentKey = "staff";
                currentLabel = "Staff users";
                var existingStaffUsers = await apiClient.GetStaffUsersAsync(branchId, cancellationToken);
                var existingStaffUserNames = existingStaffUsers
                    .Select(staffUser => staffUser.UserName.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var staffUser in StaffUsers)
                {
                    var userName = staffUser.UserName.Trim();
                    currentKey = $"staff:{userName}";
                    currentLabel = $"Staff {userName}";

                    if (existingStaffUserNames.Contains(userName))
                    {
                        AddResult(currentKey, currentLabel, "reused", $"Reused staff user {userName}.", null);
                        continue;
                    }

                    var created = await apiClient.CreateStaffUserAsync(
                        branchId,
                        new CreateStaffUserRequest(
                            organizationId,
                            userName,
                            staffUser.DisplayName.Trim(),
                            staffUser.Password,
                            [staffUser.RoleName.Trim()]),
                        cancellationToken);

                    AddResult(
                        currentKey,
                        currentLabel,
                        "created",
                        $"Created staff user {created.UserName}.",
                        created.StaffUserId.ToString("D"));
                }
            }

            if (CanSetupLayout)
            {
                var zoneNameValue = ZoneName.Trim();
                currentKey = $"zone:{zoneNameValue}";
                currentLabel = $"Zone {zoneNameValue}";
                var existingZones = await apiClient.GetLayoutZonesAsync(branchId, cancellationToken);
                var zone = existingZones.FirstOrDefault(zoneDto =>
                    string.Equals(zoneDto.Name.Trim(), zoneNameValue, StringComparison.OrdinalIgnoreCase));

                if (zone is null)
                {
                    zone = await apiClient.CreateZoneAsync(
                        branchId,
                        new CreateZoneRequest(organizationId, zoneNameValue, SeatSortOrderStart),
                        cancellationToken);
                    AddResult(currentKey, currentLabel, "created", $"Created zone {zone.Name}.", zone.ZoneId.ToString("D"));
                }
                else
                {
                    AddResult(currentKey, currentLabel, "reused", $"Reused zone {zone.Name}.", zone.ZoneId.ToString("D"));
                    foreach (var seat in zone.Seats)
                    {
                        seatsByName[seat.Name.Trim()] = seat;
                    }
                }

                foreach (var seatName in BuildSeatNames())
                {
                    currentKey = $"seat:{seatName}";
                    currentLabel = $"Seat {seatName}";

                    if (seatsByName.TryGetValue(seatName, out var existingSeat))
                    {
                        AddResult(currentKey, currentLabel, "reused", $"Reused seat {seatName}.", existingSeat.SeatId.ToString("D"));
                        continue;
                    }

                    var seat = await apiClient.CreateSeatAsync(
                        branchId,
                        new CreateSeatRequest(
                            organizationId,
                            zone.ZoneId,
                            seatName,
                            SeatSortOrderStart + seatsByName.Count),
                        cancellationToken);
                    seatsByName[seatName] = seat;
                    AddResult(currentKey, currentLabel, "created", $"Created seat {seat.Name}.", seat.SeatId.ToString("D"));
                }
            }

            TariffDto? tariff = null;

            if (CanSetupTariff)
            {
                var tariffNameValue = TariffName.Trim();
                currentKey = "tariff";
                currentLabel = "Tariff";
                tariff = await apiClient.CreateTariffAsync(
                    branchId,
                    new CreateTariffRequest(
                        organizationId,
                        tariffNameValue,
                        CreateIdempotencyKey(branchId, "tariff", tariffNameValue)),
                    cancellationToken);
                AddResult(currentKey, currentLabel, "created", $"Created tariff {tariff.Name}.", tariff.TariffId.ToString("D"));

                currentKey = "tariff-version";
                currentLabel = "Tariff version";
                var tariffVersion = await apiClient.CreateTariffVersionAsync(
                    branchId,
                    tariff.TariffId,
                    new CreateTariffVersionRequest(
                        organizationId,
                        tariff.TariffId,
                        CurrencyCode.Trim(),
                        PricePerMinuteMinorUnits,
                        MinimumBillableMinutes,
                        RoundingIncrementMinutes,
                        EffectiveFromUtc,
                        CreateIdempotencyKey(
                            branchId,
                            "tariff-version",
                            tariffNameValue,
                            CurrencyCode,
                            PricePerMinuteMinorUnits.ToString(),
                            MinimumBillableMinutes.ToString(),
                            RoundingIncrementMinutes.ToString(),
                            EffectiveFromUtc.ToString("O"))),
                    cancellationToken);
                AddResult(
                    currentKey,
                    currentLabel,
                    "created",
                    $"Created tariff version {tariffVersion.CurrencyCode}.",
                    tariffVersion.TariffVersionId.ToString("D"));
            }

            PosProductCategoryDto? category = null;

            if (CanSetupPos)
            {
                currentKey = "pos-category";
                currentLabel = "POS category";
                category = await apiClient.CreateProductCategoryAsync(
                    branchId,
                    new CreateProductCategoryRequest(
                        organizationId,
                        ProductCategoryName.Trim(),
                        CreateIdempotencyKey(branchId, "pos-category", ProductCategoryName)),
                    cancellationToken);
                AddResult(
                    currentKey,
                    currentLabel,
                    "created",
                    $"Created POS category {category.Name}.",
                    category.CategoryId.ToString("D"));

                currentKey = "pos-product";
                currentLabel = "POS product";
                var product = await apiClient.CreateProductAsync(
                    branchId,
                    new CreateProductRequest(
                        organizationId,
                        category.CategoryId,
                        ProductName.Trim(),
                        ProductSku.Trim(),
                        new MoneyDto(CurrencyCode.Trim(), ProductPriceMinorUnits),
                        ProductTrackStock,
                        ProductAllowNegativeStock,
                        CreateIdempotencyKey(
                            branchId,
                            "pos-product",
                            ProductCategoryName,
                            category.CategoryId.ToString("D"),
                            ProductName,
                            ProductSku,
                            CurrencyCode,
                            ProductPriceMinorUnits.ToString(),
                            ProductTrackStock.ToString(),
                            ProductAllowNegativeStock.ToString())),
                    cancellationToken);
                AddResult(
                    currentKey,
                    currentLabel,
                    "created",
                    $"Created POS product {product.Sku}.",
                    product.ProductId.ToString("D"));
            }

            if (CanAssignDeviceSeat && !string.IsNullOrWhiteSpace(DeviceIdText))
            {
                currentKey = "device-assignment";
                currentLabel = "Device assignment";
                var targetSeatName = string.IsNullOrWhiteSpace(TargetAssignmentSeatName)
                    ? BuildSeatNames().FirstOrDefault()
                    : TargetAssignmentSeatName.Trim();

                if (seatsByName.Count == 0)
                {
                    var zones = await apiClient.GetLayoutZonesAsync(branchId, cancellationToken);
                    foreach (var seat in zones.SelectMany(zone => zone.Seats))
                    {
                        seatsByName[seat.Name.Trim()] = seat;
                    }
                }

                if (targetSeatName is null || !seatsByName.TryGetValue(targetSeatName, out var targetSeat))
                {
                    throw new InvalidOperationException("Target seat could not be resolved.");
                }

                var assignment = await apiClient.AssignDeviceSeatAsync(
                    branchId,
                    Guid.Parse(DeviceIdText.Trim()),
                    new AssignDeviceSeatRequest(organizationId, targetSeat.SeatId),
                    cancellationToken);
                AddResult(
                    currentKey,
                    currentLabel,
                    "created",
                    $"Assigned device to seat {targetSeatName}.",
                    assignment.DeviceSeatAssignmentId.ToString("D"));
            }

            StatusMessage = "Pilot setup applied.";
            ErrorMessage = null;
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = RedactSensitive(exception.Message);
            AddResult(currentKey, currentLabel, "failed", ErrorMessage, null);
            StatusMessage = "Pilot setup failed.";
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = RedactSensitive(exception.Message);
            AddResult(currentKey, currentLabel, "failed", ErrorMessage, null);
            StatusMessage = "Pilot setup failed.";
        }
        catch (TaskCanceledException exception)
        {
            ErrorMessage = RedactSensitive(exception.Message);
            AddResult(currentKey, currentLabel, "failed", ErrorMessage, null);
            StatusMessage = "Pilot setup failed.";
        }
        catch (JsonException exception)
        {
            ErrorMessage = RedactSensitive(exception.Message);
            AddResult(currentKey, currentLabel, "failed", ErrorMessage, null);
            StatusMessage = "Pilot setup failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryValidate(out Guid organizationId, out Guid branchId)
    {
        organizationId = default;
        branchId = default;

        if (!Guid.TryParse(OrganizationIdText, out organizationId))
        {
            ErrorMessage = "OrganizationId must be a valid GUID.";
            AddResult("context", "Tenant context", "failed", ErrorMessage, null);
            return false;
        }

        if (!Guid.TryParse(BranchIdText, out branchId))
        {
            ErrorMessage = "BranchId must be a valid GUID.";
            AddResult("context", "Tenant context", "failed", ErrorMessage, null);
            return false;
        }

        if (CanSetupStaff)
        {
            foreach (var staffUser in StaffUsers)
            {
                if (string.IsNullOrWhiteSpace(staffUser.UserName)
                    || string.IsNullOrWhiteSpace(staffUser.DisplayName)
                    || string.IsNullOrWhiteSpace(staffUser.Password)
                    || string.IsNullOrWhiteSpace(staffUser.RoleName))
                {
                    ErrorMessage = "Staff setup rows require username, display name, password, and role.";
                    AddResult("staff", "Staff users", "failed", ErrorMessage, null);
                    return false;
                }
            }
        }

        if (CanSetupLayout)
        {
            if (string.IsNullOrWhiteSpace(ZoneName)
                || string.IsNullOrWhiteSpace(SeatPrefix)
                || SeatCount is < 1 or > 200
                || SeatSortOrderStart < 1)
            {
                ErrorMessage = "Layout setup requires zone, seat prefix, 1-200 seats, and positive sort order.";
                AddResult("layout", "Layout", "failed", ErrorMessage, null);
                return false;
            }
        }

        if (CanSetupTariff)
        {
            if (string.IsNullOrWhiteSpace(TariffName)
                || string.IsNullOrWhiteSpace(CurrencyCode)
                || PricePerMinuteMinorUnits <= 0
                || MinimumBillableMinutes <= 0
                || RoundingIncrementMinutes <= 0)
            {
                ErrorMessage = "Tariff setup requires name, currency, and positive pricing values.";
                AddResult("tariff", "Tariff", "failed", ErrorMessage, null);
                return false;
            }
        }

        if (CanSetupPos)
        {
            if (string.IsNullOrWhiteSpace(ProductCategoryName)
                || string.IsNullOrWhiteSpace(ProductName)
                || string.IsNullOrWhiteSpace(ProductSku)
                || string.IsNullOrWhiteSpace(CurrencyCode)
                || ProductPriceMinorUnits <= 0)
            {
                ErrorMessage = "POS setup requires category, product, SKU, currency, and positive price.";
                AddResult("pos", "POS catalog", "failed", ErrorMessage, null);
                return false;
            }
        }

        if (CanAssignDeviceSeat
            && !string.IsNullOrWhiteSpace(DeviceIdText)
            && !Guid.TryParse(DeviceIdText, out _))
        {
            ErrorMessage = "DeviceId must be a valid GUID.";
            AddResult("device-assignment", "Device assignment", "failed", ErrorMessage, null);
            return false;
        }

        return true;
    }

    private IReadOnlyList<string> BuildSeatNames()
    {
        return Enumerable.Range(1, SeatCount)
            .Select(index => $"{SeatPrefix.Trim()}{index:000}")
            .ToList();
    }

    private string CreateIdempotencyKey(Guid branchId, params string[] values)
    {
        return string.Join(
            ":",
            new[] { "pilot-setup", branchId.ToString("D") }
                .Concat(values.Select(NormalizeIdempotencyValue)));
    }

    private void AddResult(string key, string label, string state, string detail, string? entityId)
    {
        Results.Add(new PilotSetupStepResultViewModel(
            key,
            label,
            state,
            RedactSensitive(detail),
            entityId));
    }

    private string RedactSensitive(string value)
    {
        var redacted = value;

        foreach (var password in passwordRedactionValues)
        {
            redacted = redacted.Replace(password, "[redacted]", StringComparison.Ordinal);
        }

        return redacted;
    }

    private IReadOnlyList<string> CreatePasswordRedactionValues()
    {
        return StaffUsers
            .SelectMany(staffUser =>
            {
                if (string.IsNullOrEmpty(staffUser.Password))
                {
                    return [];
                }

                return new[]
                {
                    staffUser.Password,
                    JsonEncodedText.Encode(staffUser.Password).ToString(),
                    JsonSerializer.Serialize(staffUser.Password).Trim('"')
                };
            })
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToList();
    }

    private static string NormalizeIdempotencyValue(string value)
    {
        var trimmed = value.Trim();
        return DateTimeOffset.TryParse(
            trimmed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out _)
            ? trimmed
            : trimmed.ToLowerInvariant();
    }

    private bool CanRunCommand()
    {
        return !IsBusy && HasAnySetupPermission;
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
