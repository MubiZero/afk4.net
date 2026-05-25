using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

public sealed class SetupWizardViewModel(
    ISetupWizardApiClient apiClient,
    IDeviceKeyStore deviceKeyStore,
    ISetupWizardBootstrapWriter bootstrapWriter,
    SetupWizardMachineInfo machineInfo) : INotifyPropertyChanged
{
    private readonly ISetupWizardApiClient apiClient = apiClient;
    private readonly IDeviceKeyStore deviceKeyStore = deviceKeyStore;
    private readonly ISetupWizardBootstrapWriter bootstrapWriter = bootstrapWriter;
    private readonly SetupWizardMachineInfo machineInfo = machineInfo;
    private string ownerCode = string.Empty;
    private string displayName = machineInfo.MachineName;
    private string selectedRole = DeviceRoleNames.GamingPc;
    private SetupWizardStep currentStep = SetupWizardStep.OwnerCode;
    private string statusMessage = "Enter owner code.";
    private string? errorMessage;
    private string normalizedOwnerCode = string.Empty;
    private SetupWizardBranchViewModel? selectedBranch;
    private SetupWizardSeatViewModel? selectedSeat;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string OwnerCode
    {
        get => ownerCode;
        set => SetField(ref ownerCode, value);
    }

    public string DisplayName
    {
        get => displayName;
        set => SetField(ref displayName, value);
    }

    public string SelectedRole
    {
        get => selectedRole;
        set => SetField(ref selectedRole, value);
    }

    public SetupWizardStep CurrentStep
    {
        get => currentStep;
        private set => SetField(ref currentStep, value);
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

    public ObservableCollection<SetupWizardBranchViewModel> Branches { get; } = [];

    public ObservableCollection<SetupWizardSeatViewModel> Seats { get; } = [];

    public SetupWizardBranchViewModel? SelectedBranch
    {
        get => selectedBranch;
        private set => SetField(ref selectedBranch, value);
    }

    public SetupWizardSeatViewModel? SelectedSeat
    {
        get => selectedSeat;
        private set => SetField(ref selectedSeat, value);
    }

    public async Task DiscoverAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!TryNormalizeOwnerCode(OwnerCode, out normalizedOwnerCode))
        {
            ErrorMessage = "Owner code must contain only digits, spaces, or dashes.";
            CurrentStep = SetupWizardStep.OwnerCode;
            return;
        }

        if (normalizedOwnerCode.Length != 8)
        {
            ErrorMessage = "Owner code must be exactly 8 digits.";
            CurrentStep = SetupWizardStep.OwnerCode;
            return;
        }

        var response = await apiClient.DiscoverAsync(normalizedOwnerCode, cancellationToken);

        Branches.Clear();
        Seats.Clear();
        SelectedBranch = null;
        SelectedSeat = null;
        foreach (var branch in response.Branches.OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase))
        {
            Branches.Add(new SetupWizardBranchViewModel(branch));
        }

        CurrentStep = SetupWizardStep.BranchSelection;
        StatusMessage = $"Owner: {response.OwnerDisplayName}";
    }

    public void SelectBranch(SetupWizardBranchViewModel branch)
    {
        ErrorMessage = null;
        SelectedBranch = branch;
        SelectedSeat = null;
        Seats.Clear();

        var freeSeatIds = branch.FreeSeatIds.ToHashSet();
        foreach (var seat in branch.FloorMap.Seats
            .OrderBy(seat => ZoneSortOrder(branch.FloorMap, seat.ZoneId))
            .ThenBy(seat => seat.SortOrder)
            .ThenBy(seat => seat.SeatName, StringComparer.OrdinalIgnoreCase))
        {
            Seats.Add(new SetupWizardSeatViewModel(
                seat.SeatId,
                seat.ZoneId,
                seat.ZoneName,
                seat.SeatName,
                seat.SortOrder,
                seat.State,
                freeSeatIds.Contains(seat.SeatId)));
        }

        CurrentStep = SetupWizardStep.SeatSelection;
        StatusMessage = $"Branch: {branch.Name}";
    }

    public void SelectSeat(SetupWizardSeatViewModel seat)
    {
        if (!seat.IsSelectable)
        {
            ErrorMessage = "Choose a free seat.";
            return;
        }

        ErrorMessage = null;
        SelectedSeat = seat;
        CurrentStep = SetupWizardStep.RoleSelection;
        StatusMessage = $"Seat: {seat.Name}";
    }

    public async Task CreateSeatAsync(string name, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (SelectedBranch is null)
        {
            ErrorMessage = "Choose a branch first.";
            return;
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            ErrorMessage = "Seat name is required.";
            return;
        }

        var zone = SelectedBranch.FloorMap.Zones
            .OrderBy(candidate => candidate.SortOrder)
            .FirstOrDefault();
        if (zone is null)
        {
            ErrorMessage = "This branch has no zone to place the seat in.";
            return;
        }

        var created = await apiClient.CreateSeatAsync(
            normalizedOwnerCode,
            SelectedBranch.BranchId,
            zone.ZoneId,
            trimmedName,
            cancellationToken);

        var seat = new SetupWizardSeatViewModel(
            created.SeatId,
            created.ZoneId,
            zone.Name,
            created.Name,
            created.SortOrder,
            "Free",
            IsSelectable: true);
        Seats.Add(seat);
        SelectSeat(seat);
    }

    public async Task EnrollAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (SelectedBranch is null || SelectedSeat is null)
        {
            ErrorMessage = "Choose a branch and free seat before enrolling.";
            return;
        }

        var displayNameValue = string.IsNullOrWhiteSpace(DisplayName)
            ? machineInfo.MachineName
            : DisplayName.Trim();
        var publicKey = await deviceKeyStore.GetOrCreatePublicKeyPemAsync(cancellationToken);
        var response = await apiClient.EnrollAsync(
            new InstallEnrollRequest(
                normalizedOwnerCode,
                SelectedBranch.BranchId,
                SelectedSeat.SeatId,
                SelectedRole,
                displayNameValue,
                machineInfo.MachineName,
                publicKey),
            cancellationToken);

        bootstrapWriter.Write(new SetupWizardBootstrapConfig(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            response.CredentialId,
            response.CredentialSecret,
            SelectedRole,
            response.ApiBaseUrl,
            response.UpdateChannel,
            response.LeaseSigningPublicKeyPem,
            response.UpdatePackageSigningPublicKeyPem));

        StatusMessage = response.EnrollmentState == DeviceEnrollmentStateNames.Pending
            ? "Enrollment pending approval in the club dashboard."
            : "Device enrolled and ready for Agent startup.";
        CurrentStep = SetupWizardStep.Finished;
    }

    public void ReportError(string message)
    {
        ErrorMessage = message;
        StatusMessage = "Setup action failed.";
    }

    private static bool TryNormalizeOwnerCode(string value, out string normalized)
    {
        var digits = new char[value.Length];
        var digitCount = 0;
        foreach (var character in value)
        {
            if (character is >= '0' and <= '9')
            {
                digits[digitCount] = character;
                digitCount++;
                continue;
            }

            if (char.IsWhiteSpace(character) || character == '-')
            {
                continue;
            }

            normalized = string.Empty;
            return false;
        }

        normalized = new string(digits, 0, digitCount);
        return true;
    }

    private static int ZoneSortOrder(FloorMapDto floorMap, Guid zoneId) =>
        floorMap.Zones.FirstOrDefault(zone => zone.ZoneId == zoneId)?.SortOrder ?? int.MaxValue;

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

public sealed class SetupWizardBranchViewModel(InstallBranchDto branch)
{
    public Guid BranchId { get; } = branch.BranchId;

    public string Slug { get; } = branch.Slug;

    public string Name { get; } = branch.Name;

    public FloorMapDto FloorMap { get; } = branch.FloorMap;

    public IReadOnlyList<Guid> FreeSeatIds { get; } = branch.FreeSeatIds;
}

public sealed record SetupWizardSeatViewModel(
    Guid SeatId,
    Guid ZoneId,
    string ZoneName,
    string Name,
    int SortOrder,
    string State,
    bool IsSelectable);
