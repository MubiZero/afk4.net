using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private bool isBusy;

    public UpdateStatusWorkspaceViewModel(IOperatorUpdateApiClient apiClient)
    {
        this.apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UpdateRolloutStatusViewModel> Rollouts { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }

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
            }
        }
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
