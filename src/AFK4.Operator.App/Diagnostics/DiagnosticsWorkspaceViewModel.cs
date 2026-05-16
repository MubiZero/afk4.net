using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.Operator.App.Diagnostics;

public sealed class DiagnosticsWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IOperatorDiagnosticsApiClient apiClient;
    private string organizationIdText = string.Empty;
    private string branchIdText = string.Empty;
    private string summary = "Not loaded";
    private string generatedAtUtcText = string.Empty;
    private string statusMessage = string.Empty;
    private string? errorMessage;
    private int totalDevices;
    private int onlineDevices;
    private int lockedDevices;
    private int staleDeviceCount;
    private int pendingCommandCount;
    private int failedCommandCount;
    private int activeRolloutCount;
    private int installingDeviceCount;
    private int failedUpdateCount;
    private int rollbackDeviceCount;
    private bool isBusy;

    public DiagnosticsWorkspaceViewModel(IOperatorDiagnosticsApiClient apiClient)
    {
        this.apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<StaleDeviceDiagnosticsViewModel> StaleDevices { get; } = [];

    public ObservableCollection<FailedCommandDiagnosticsViewModel> FailedCommands { get; } = [];

    public ObservableCollection<FailedUpdateDiagnosticsViewModel> FailedUpdates { get; } = [];

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

    public string GeneratedAtUtcText
    {
        get => generatedAtUtcText;
        private set => SetField(ref generatedAtUtcText, value);
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

    public int TotalDevices
    {
        get => totalDevices;
        private set => SetField(ref totalDevices, value);
    }

    public int OnlineDevices
    {
        get => onlineDevices;
        private set => SetField(ref onlineDevices, value);
    }

    public int LockedDevices
    {
        get => lockedDevices;
        private set => SetField(ref lockedDevices, value);
    }

    public int StaleDeviceCount
    {
        get => staleDeviceCount;
        private set => SetField(ref staleDeviceCount, value);
    }

    public int PendingCommandCount
    {
        get => pendingCommandCount;
        private set => SetField(ref pendingCommandCount, value);
    }

    public int FailedCommandCount
    {
        get => failedCommandCount;
        private set => SetField(ref failedCommandCount, value);
    }

    public int ActiveRolloutCount
    {
        get => activeRolloutCount;
        private set => SetField(ref activeRolloutCount, value);
    }

    public int InstallingDeviceCount
    {
        get => installingDeviceCount;
        private set => SetField(ref installingDeviceCount, value);
    }

    public int FailedUpdateCount
    {
        get => failedUpdateCount;
        private set => SetField(ref failedUpdateCount, value);
    }

    public int RollbackDeviceCount
    {
        get => rollbackDeviceCount;
        private set => SetField(ref rollbackDeviceCount, value);
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
        if (!Guid.TryParse(BranchIdText, out var branchId))
        {
            ErrorMessage = "BranchId must be a valid GUID.";
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var diagnostics = await apiClient.GetDiagnosticsAsync(branchId, cancellationToken);

            ApplyDiagnostics(diagnostics);
            StatusMessage = "Diagnostics loaded.";
        });
    }

    private void ApplyDiagnostics(BranchDiagnosticsDto diagnostics)
    {
        TotalDevices = diagnostics.DeviceSummary.TotalDevices;
        OnlineDevices = diagnostics.DeviceSummary.OnlineDevices;
        LockedDevices = diagnostics.DeviceSummary.LockedDevices;
        StaleDeviceCount = diagnostics.DeviceSummary.StaleDevices;
        PendingCommandCount = diagnostics.CommandSummary.PendingCommands;
        FailedCommandCount = diagnostics.CommandSummary.FailedCommands;
        ActiveRolloutCount = diagnostics.UpdateSummary.ActiveRollouts;
        InstallingDeviceCount = diagnostics.UpdateSummary.InstallingDevices;
        FailedUpdateCount = diagnostics.UpdateSummary.FailedDevices;
        RollbackDeviceCount = diagnostics.UpdateSummary.RollbackDevices;
        GeneratedAtUtcText = $"Generated {diagnostics.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC";
        Summary = $"{TotalDevices} devices, {StaleDeviceCount} stale, {FailedCommandCount} failed commands, {FailedUpdateCount} failed updates.";

        StaleDevices.Clear();
        foreach (var device in diagnostics.StaleDevices)
        {
            StaleDevices.Add(new StaleDeviceDiagnosticsViewModel(device));
        }

        FailedCommands.Clear();
        foreach (var command in diagnostics.CommandSummary.RecentFailures)
        {
            FailedCommands.Add(new FailedCommandDiagnosticsViewModel(command));
        }

        FailedUpdates.Clear();
        foreach (var update in diagnostics.UpdateSummary.RecentFailures)
        {
            FailedUpdates.Add(new FailedUpdateDiagnosticsViewModel(update));
        }
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

public sealed class StaleDeviceDiagnosticsViewModel
{
    public StaleDeviceDiagnosticsViewModel(StaleDeviceDiagnosticsDto device)
    {
        DeviceId = device.DeviceId;
        DeviceIdText = device.DeviceId.ToString("D");
        MachineName = device.MachineName;
        AgentVersion = device.AgentVersion;
        ShellVersion = device.ShellVersion;
        IsOnline = device.IsOnline;
        IsLocked = device.IsLocked;
        LastHeartbeatAtUtc = device.LastHeartbeatAtUtc;
        LastHeartbeatAgeSeconds = device.LastHeartbeatAgeSeconds;
        StateSummary = $"{(device.IsOnline ? "Online" : "Offline")}, {(device.IsLocked ? "locked" : "unlocked")}";
        VersionSummary = $"Agent {device.AgentVersion}, Shell {device.ShellVersion}";
        HeartbeatSummary = device.LastHeartbeatAtUtc is null
            ? "No heartbeat"
            : $"{device.LastHeartbeatAtUtc:yyyy-MM-dd HH:mm:ss} UTC ({device.LastHeartbeatAgeSeconds}s ago)";
    }

    public Guid DeviceId { get; }

    public string DeviceIdText { get; }

    public string MachineName { get; }

    public string AgentVersion { get; }

    public string ShellVersion { get; }

    public bool IsOnline { get; }

    public bool IsLocked { get; }

    public DateTimeOffset? LastHeartbeatAtUtc { get; }

    public long? LastHeartbeatAgeSeconds { get; }

    public string StateSummary { get; }

    public string VersionSummary { get; }

    public string HeartbeatSummary { get; }
}

public sealed class FailedCommandDiagnosticsViewModel
{
    public FailedCommandDiagnosticsViewModel(FailedCommandDiagnosticsDto command)
    {
        DeviceId = command.DeviceId;
        MachineName = command.MachineName;
        CommandId = command.CommandId;
        CommandIdText = command.CommandId.ToString("D");
        Type = command.Type;
        Status = command.Status;
        Message = command.Message ?? string.Empty;
        UpdatedAtUtc = command.UpdatedAtUtc;
    }

    public Guid DeviceId { get; }

    public string MachineName { get; }

    public Guid CommandId { get; }

    public string CommandIdText { get; }

    public string Type { get; }

    public string Status { get; }

    public string Message { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed class FailedUpdateDiagnosticsViewModel
{
    public FailedUpdateDiagnosticsViewModel(FailedUpdateDiagnosticsDto update)
    {
        DeviceId = update.DeviceId;
        MachineName = update.MachineName;
        UpdateRolloutId = update.UpdateRolloutId;
        UpdateRolloutIdText = update.UpdateRolloutId.ToString("D");
        Component = update.Component;
        TargetVersion = update.TargetVersion;
        Status = update.Status;
        Message = update.Message;
        UpdatedAtUtc = update.UpdatedAtUtc;
    }

    public Guid DeviceId { get; }

    public string MachineName { get; }

    public Guid UpdateRolloutId { get; }

    public string UpdateRolloutIdText { get; }

    public string Component { get; }

    public string TargetVersion { get; }

    public string Status { get; }

    public string Message { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}
