using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Operator.App.Updates;

public sealed class UpdateStatusWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IOperatorUpdateApiClient apiClient;
    private string organizationIdText = "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08";
    private string branchIdText = "acfc0212-967f-4d84-94be-9003387b09c2";
    private string summary = "Not loaded";
    private string statusMessage = string.Empty;
    private string? errorMessage;
    private string componentText = UpdateComponentNames.AgentService;
    private string versionText = "1.0.0";
    private string channelText = UpdateChannelNames.Internal;
    private string artifactUriText = string.Empty;
    private string sha256Text = string.Empty;
    private string signatureText = string.Empty;
    private string signatureAlgorithmText = UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363;
    private string sizeBytesText = "0";
    private string releaseNotesText = string.Empty;
    private string lastPackageIdText = string.Empty;
    private string packageStatePackageIdText = string.Empty;
    private string packageStateText = UpdatePackageStateNames.Validated;
    private string packageStateReasonText = string.Empty;
    private string rolloutPackageIdText = string.Empty;
    private string rolloutChannelText = UpdateChannelNames.Internal;
    private string targetKindText = UpdateTargetKindNames.Branch;
    private string targetDeviceIdsText = string.Empty;
    private string batchPercentText = "100";
    private string startsAtUtcText = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private string rolloutReasonText = string.Empty;
    private string lastRolloutIdText = string.Empty;
    private string rolloutStateRolloutIdText = string.Empty;
    private string rolloutStateText = UpdateRolloutStateNames.Paused;
    private string rolloutStateReasonText = string.Empty;
    private bool isBusy;

    public UpdateStatusWorkspaceViewModel(IOperatorUpdateApiClient apiClient)
    {
        this.apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
        RegisterPackageCommand = new AsyncRelayCommand(RegisterPackageAsync, CanRunCommand);
        ChangePackageStateCommand = new AsyncRelayCommand(ChangePackageStateAsync, CanRunCommand);
        CreateRolloutCommand = new AsyncRelayCommand(CreateRolloutAsync, CanRunCommand);
        ChangeRolloutStateCommand = new AsyncRelayCommand(ChangeRolloutStateAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UpdateRolloutStatusViewModel> Rollouts { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RegisterPackageCommand { get; }

    public AsyncRelayCommand ChangePackageStateCommand { get; }

    public AsyncRelayCommand CreateRolloutCommand { get; }

    public AsyncRelayCommand ChangeRolloutStateCommand { get; }

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

    public string Summary
    {
        get => summary;
        private set => SetField(ref summary, value);
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

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                RegisterPackageCommand.NotifyCanExecuteChanged();
                ChangePackageStateCommand.NotifyCanExecuteChanged();
                CreateRolloutCommand.NotifyCanExecuteChanged();
                ChangeRolloutStateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ComponentText
    {
        get => componentText;
        set => SetField(ref componentText, value);
    }

    public string VersionText
    {
        get => versionText;
        set => SetField(ref versionText, value);
    }

    public string ChannelText
    {
        get => channelText;
        set => SetField(ref channelText, value);
    }

    public string ArtifactUriText
    {
        get => artifactUriText;
        set => SetField(ref artifactUriText, value);
    }

    public string Sha256Text
    {
        get => sha256Text;
        set => SetField(ref sha256Text, value);
    }

    public string SignatureText
    {
        get => signatureText;
        set => SetField(ref signatureText, value);
    }

    public string SignatureAlgorithmText
    {
        get => signatureAlgorithmText;
        set => SetField(ref signatureAlgorithmText, value);
    }

    public string SizeBytesText
    {
        get => sizeBytesText;
        set => SetField(ref sizeBytesText, value);
    }

    public string ReleaseNotesText
    {
        get => releaseNotesText;
        set => SetField(ref releaseNotesText, value);
    }

    public string LastPackageIdText
    {
        get => lastPackageIdText;
        private set => SetField(ref lastPackageIdText, value);
    }

    public string PackageStatePackageIdText
    {
        get => packageStatePackageIdText;
        set => SetField(ref packageStatePackageIdText, value);
    }

    public string PackageStateText
    {
        get => packageStateText;
        set => SetField(ref packageStateText, value);
    }

    public string PackageStateReasonText
    {
        get => packageStateReasonText;
        set => SetField(ref packageStateReasonText, value);
    }

    public string RolloutPackageIdText
    {
        get => rolloutPackageIdText;
        set => SetField(ref rolloutPackageIdText, value);
    }

    public string RolloutChannelText
    {
        get => rolloutChannelText;
        set => SetField(ref rolloutChannelText, value);
    }

    public string TargetKindText
    {
        get => targetKindText;
        set => SetField(ref targetKindText, value);
    }

    public string TargetDeviceIdsText
    {
        get => targetDeviceIdsText;
        set => SetField(ref targetDeviceIdsText, value);
    }

    public string BatchPercentText
    {
        get => batchPercentText;
        set => SetField(ref batchPercentText, value);
    }

    public string StartsAtUtcText
    {
        get => startsAtUtcText;
        set => SetField(ref startsAtUtcText, value);
    }

    public string RolloutReasonText
    {
        get => rolloutReasonText;
        set => SetField(ref rolloutReasonText, value);
    }

    public string LastRolloutIdText
    {
        get => lastRolloutIdText;
        private set => SetField(ref lastRolloutIdText, value);
    }

    public string RolloutStateRolloutIdText
    {
        get => rolloutStateRolloutIdText;
        set => SetField(ref rolloutStateRolloutIdText, value);
    }

    public string RolloutStateText
    {
        get => rolloutStateText;
        set => SetField(ref rolloutStateText, value);
    }

    public string RolloutStateReasonText
    {
        get => rolloutStateReasonText;
        set => SetField(ref rolloutStateReasonText, value);
    }

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        OrganizationIdText = organizationId.ToString("D");
        BranchIdText = branchId.ToString("D");
    }

    public Task RefreshAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(BranchIdText, "BranchId", out var branchId))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var rollouts = await apiClient.GetRolloutStatusesAsync(branchId, cancellationToken);

            Rollouts.Clear();
            foreach (var rollout in rollouts)
            {
                Rollouts.Add(new UpdateRolloutStatusViewModel(rollout));
            }

            Summary = rollouts.Count == 0
                ? "No update rollouts."
                : $"{rollouts.Count} update rollout{(rollouts.Count == 1 ? string.Empty : "s")}, {rollouts.Count(rollout => rollout.State == UpdateRolloutStateNames.Active)} active.";
            StatusMessage = $"{rollouts.Count} update rollout{(rollouts.Count == 1 ? string.Empty : "s")} loaded.";
        });
    }

    public Task RegisterPackageAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(OrganizationIdText, "OrganizationId", out var organizationId) ||
            !TryParseGuid(BranchIdText, "BranchId", out var branchId) ||
            !TryParseLong(SizeBytesText, "SizeBytes", out var sizeBytes))
        {
            return Task.CompletedTask;
        }

        if (!TryRequireText(ComponentText, "Component", out var component) ||
            !TryRequireText(VersionText, "Version", out var version) ||
            !TryRequireText(ChannelText, "Channel", out var channel) ||
            !TryRequireText(ArtifactUriText, "ArtifactUri", out var artifactUri) ||
            !TryRequireText(Sha256Text, "Sha256", out var sha256) ||
            !TryRequireText(SignatureText, "Signature", out var signature) ||
            !TryRequireText(SignatureAlgorithmText, "SignatureAlgorithm", out var signatureAlgorithm) ||
            !TryRequireText(ReleaseNotesText, "ReleaseNotes", out var releaseNotes))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var request = new CreateUpdatePackageRequest(
                organizationId,
                component,
                version,
                channel,
                artifactUri,
                sha256,
                signature,
                signatureAlgorithm,
                sizeBytes,
                releaseNotes);
            var package = await apiClient.RegisterPackageAsync(branchId, request, cancellationToken);
            LastPackageIdText = package.UpdatePackageId.ToString("D");
            PackageStatePackageIdText = LastPackageIdText;
            RolloutPackageIdText = LastPackageIdText;
            RolloutChannelText = package.Channel;
            StatusMessage = $"Registered update package {ComponentLabel(package.Component)} {package.Version}.";
        });
    }

    public Task ChangePackageStateAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(OrganizationIdText, "OrganizationId", out var organizationId) ||
            !TryParseGuid(BranchIdText, "BranchId", out var branchId) ||
            !TryParseGuid(PackageStatePackageIdText, "UpdatePackageId", out var updatePackageId))
        {
            return Task.CompletedTask;
        }

        if (!TryRequireText(PackageStateText, "PackageState", out var state) ||
            !TryRequireText(PackageStateReasonText, "PackageStateReason", out var reason))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var request = new UpdatePackageStateChangeRequest(organizationId, state, reason);
            var package = await apiClient.ChangePackageStateAsync(branchId, updatePackageId, request, cancellationToken);
            LastPackageIdText = package.UpdatePackageId.ToString("D");
            StatusMessage = $"Package state changed to {package.State}.";
        });
    }

    public Task CreateRolloutAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(OrganizationIdText, "OrganizationId", out var organizationId) ||
            !TryParseGuid(BranchIdText, "BranchId", out var branchId) ||
            !TryParseGuid(RolloutPackageIdText, "UpdatePackageId", out var updatePackageId) ||
            !TryParseInt(BatchPercentText, "BatchPercent", out var batchPercent) ||
            !TryParseDateTimeOffset(StartsAtUtcText, "StartsAtUtc", out var startsAtUtc))
        {
            return Task.CompletedTask;
        }

        if (!TryRequireText(RolloutChannelText, "RolloutChannel", out var channel) ||
            !TryRequireText(TargetKindText, "TargetKind", out var targetKind) ||
            !TryRequireText(RolloutReasonText, "RolloutReason", out var reason) ||
            !TryParseTargetDeviceIds(TargetDeviceIdsText, out var targetDeviceIds))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var request = new CreateUpdateRolloutRequest(
                organizationId,
                updatePackageId,
                channel,
                targetKind,
                targetDeviceIds,
                batchPercent,
                startsAtUtc,
                reason);
            var rollout = await apiClient.CreateRolloutAsync(branchId, request, cancellationToken);
            LastRolloutIdText = rollout.UpdateRolloutId.ToString("D");
            RolloutStateRolloutIdText = LastRolloutIdText;
            StatusMessage = $"Created update rollout {ComponentLabel(rollout.Component)} {rollout.Version}.";
        });
    }

    public Task ChangeRolloutStateAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(OrganizationIdText, "OrganizationId", out var organizationId) ||
            !TryParseGuid(BranchIdText, "BranchId", out var branchId) ||
            !TryParseGuid(RolloutStateRolloutIdText, "UpdateRolloutId", out var updateRolloutId))
        {
            return Task.CompletedTask;
        }

        if (!TryRequireText(RolloutStateText, "RolloutState", out var state) ||
            !TryRequireText(RolloutStateReasonText, "RolloutStateReason", out var reason))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var request = new UpdateRolloutStateChangeRequest(organizationId, state, reason);
            var rollout = await apiClient.ChangeRolloutStateAsync(branchId, updateRolloutId, request, cancellationToken);
            LastRolloutIdText = rollout.UpdateRolloutId.ToString("D");
            StatusMessage = $"Rollout state changed to {rollout.State}.";
        });
    }

    private async Task RunApiCallAsync(Func<Task> apiCall)
    {
        IsBusy = true;

        try
        {
            await apiCall();
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryParseGuid(string value, string fieldName, out Guid guid)
    {
        if (Guid.TryParse(value, out guid))
        {
            return true;
        }

        ErrorMessage = $"{fieldName} must be a valid GUID.";
        return false;
    }

    private bool TryParseLong(string value, string fieldName, out long number)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number >= 0)
        {
            return true;
        }

        ErrorMessage = $"{fieldName} must be a non-negative whole number.";
        return false;
    }

    private bool TryParseInt(string value, string fieldName, out int number)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number is >= 1 and <= 100)
        {
            return true;
        }

        ErrorMessage = $"{fieldName} must be between 1 and 100.";
        return false;
    }

    private bool TryParseDateTimeOffset(string value, string fieldName, out DateTimeOffset dateTimeOffset)
    {
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dateTimeOffset))
        {
            return true;
        }

        ErrorMessage = $"{fieldName} must be a valid UTC date/time.";
        return false;
    }

    private bool TryRequireText(string value, string fieldName, out string text)
    {
        text = value.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        ErrorMessage = $"{fieldName} is required.";
        return false;
    }

    private bool TryParseTargetDeviceIds(string value, out IReadOnlyList<Guid> deviceIds)
    {
        deviceIds = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parsed = new List<Guid>();
        var parts = value.Split(
            [',', ';', '\r', '\n', '\t', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (!Guid.TryParse(part, out var deviceId))
            {
                ErrorMessage = "TargetDeviceIds must contain only GUIDs.";
                return false;
            }

            parsed.Add(deviceId);
        }

        deviceIds = parsed;
        return true;
    }

    private void ClearMessages()
    {
        ErrorMessage = null;
        StatusMessage = string.Empty;
    }

    private bool CanRunCommand()
    {
        return !IsBusy;
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

    private static string ComponentLabel(string component)
    {
        return component switch
        {
            UpdateComponentNames.AgentService => "AgentService",
            UpdateComponentNames.PlayerShell => "PlayerShell",
            UpdateComponentNames.OperatorApp => "OperatorApp",
            _ => component
        };
    }
}

public sealed class UpdateRolloutStatusViewModel
{
    public UpdateRolloutStatusViewModel(UpdateRolloutStatusDto rollout)
    {
        UpdateRolloutId = rollout.UpdateRolloutId;
        UpdatePackageId = rollout.UpdatePackageId;
        Title = $"{ComponentLabel(rollout.Component)} {rollout.Version}";
        Channel = rollout.Channel;
        State = rollout.State;
        TargetSummary = rollout.TargetKind == UpdateTargetKindNames.Device
            ? $"{rollout.TargetDeviceIds.Count} devices / {rollout.BatchPercent}% batch"
            : $"Branch / {rollout.BatchPercent}% batch";
        CreatedAtUtc = rollout.CreatedAtUtc;
        StartsAtUtc = rollout.StartsAtUtc;
        CompletedAtUtc = rollout.CompletedAtUtc;
        DeviceStatuses = new ObservableCollection<DeviceUpdateStatusViewModel>(
            rollout.DeviceStatuses.Select(status => new DeviceUpdateStatusViewModel(status)));

        var installedCount = rollout.DeviceStatuses.Count(status => status.Status == UpdateStatusNames.Installed);
        var failedCount = rollout.DeviceStatuses.Count(status => status.Status == UpdateStatusNames.Failed);
        ProgressSummary = $"{installedCount} installed, {failedCount} failed, {rollout.DeviceStatuses.Count} reporting";
        ScheduleSummary = CompletedAtUtc is null
            ? $"Starts {StartsAtUtc:yyyy-MM-dd HH:mm} UTC"
            : $"Completed {CompletedAtUtc:yyyy-MM-dd HH:mm} UTC";
    }

    public Guid UpdateRolloutId { get; }

    public Guid UpdatePackageId { get; }

    public string Title { get; }

    public string Channel { get; }

    public string State { get; }

    public string TargetSummary { get; }

    public string ProgressSummary { get; }

    public string ScheduleSummary { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset StartsAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    public ObservableCollection<DeviceUpdateStatusViewModel> DeviceStatuses { get; }

    private static string ComponentLabel(string component)
    {
        return component switch
        {
            UpdateComponentNames.AgentService => "AgentService",
            UpdateComponentNames.PlayerShell => "PlayerShell",
            UpdateComponentNames.OperatorApp => "OperatorApp",
            _ => component
        };
    }
}

public sealed class DeviceUpdateStatusViewModel
{
    public DeviceUpdateStatusViewModel(DeviceUpdateStatusSnapshotDto status)
    {
        DeviceId = status.DeviceId;
        DeviceIdText = status.DeviceId.ToString("D");
        Component = status.Component;
        VersionSummary = $"{status.InstalledVersion} to {status.TargetVersion}";
        Status = status.Status;
        Message = status.Message;
        UpdatedAtUtc = status.UpdatedAtUtc;
    }

    public Guid DeviceId { get; }

    public string DeviceIdText { get; }

    public string Component { get; }

    public string VersionSummary { get; }

    public string Status { get; }

    public string Message { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}
