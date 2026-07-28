using System.Net.Http;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.OrganizationAdmin.App.Mvvm;

namespace AFK4.OrganizationAdmin.App.Devices;

public sealed class TechnicianDeviceWorkflowViewModel : INotifyPropertyChanged
{
    private readonly IOperatorDeviceApiClient apiClient;
    private string organizationIdText = "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08";
    private string branchIdText = "acfc0212-967f-4d84-94be-9003387b09c2";
    private string deviceIdText = string.Empty;
    private string credentialIdText = string.Empty;
    private string commandIdText = string.Empty;
    private int enrollmentExpiresInSeconds = 300;
    private string commandType = "lock";
    private string commandReason = "technician-request";
    private string enrollmentCode = string.Empty;
    private DateTimeOffset? enrollmentCodeExpiresAtUtc;
    private string commandStatus = "Not inspected";
    private string? commandStatusMessage;
    private string deviceName = "Not loaded";
    private string deviceSeatSummary = "Seat not loaded";
    private string deviceStateSummary = "State not loaded";
    private string deviceVersionSummary = "Versions not loaded";
    private string deviceCredentialSummary = "Active credentials: unknown";
    private string deviceInstalledAppsSummary = "Installed apps: unknown";
    private string recentCommandSummary = "Recent command: none";
    private string rotatedCredentialSecret = string.Empty;
    private string credentialStatus = "Not inspected";
    private string statusMessage = string.Empty;
    private string? errorMessage;
    private bool isBusy;

    public TechnicianDeviceWorkflowViewModel(IOperatorDeviceApiClient apiClient)
    {
        this.apiClient = apiClient;

        CreateEnrollmentCodeCommand = new AsyncRelayCommand(CreateEnrollmentCodeAsync, CanRunCommand);
        DispatchDeviceCommandCommand = new AsyncRelayCommand(DispatchDeviceCommandAsync, CanRunCommand);
        RefreshCommandStatusCommand = new AsyncRelayCommand(RefreshCommandStatusAsync, CanRunCommand);
        RefreshDeviceDetailCommand = new AsyncRelayCommand(RefreshDeviceDetailAsync, CanRunCommand);
        RotateCredentialCommand = new AsyncRelayCommand(RotateCredentialAsync, CanRunCommand);
        RevokeCredentialCommand = new AsyncRelayCommand(RevokeCredentialAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncRelayCommand CreateEnrollmentCodeCommand { get; }

    public AsyncRelayCommand DispatchDeviceCommandCommand { get; }

    public AsyncRelayCommand RefreshCommandStatusCommand { get; }

    public AsyncRelayCommand RefreshDeviceDetailCommand { get; }

    public AsyncRelayCommand RotateCredentialCommand { get; }

    public AsyncRelayCommand RevokeCredentialCommand { get; }

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

    public string DeviceIdText
    {
        get => deviceIdText;
        set => SetField(ref deviceIdText, value);
    }

    public string CredentialIdText
    {
        get => credentialIdText;
        set => SetField(ref credentialIdText, value);
    }

    public string CommandIdText
    {
        get => commandIdText;
        set => SetField(ref commandIdText, value);
    }

    public int EnrollmentExpiresInSeconds
    {
        get => enrollmentExpiresInSeconds;
        set => SetField(ref enrollmentExpiresInSeconds, value);
    }

    public string CommandType
    {
        get => commandType;
        set => SetField(ref commandType, value);
    }

    public string CommandReason
    {
        get => commandReason;
        set => SetField(ref commandReason, value);
    }

    public string EnrollmentCode
    {
        get => enrollmentCode;
        private set => SetField(ref enrollmentCode, value);
    }

    public DateTimeOffset? EnrollmentCodeExpiresAtUtc
    {
        get => enrollmentCodeExpiresAtUtc;
        private set => SetField(ref enrollmentCodeExpiresAtUtc, value);
    }

    public string CommandStatus
    {
        get => commandStatus;
        private set => SetField(ref commandStatus, value);
    }

    public string? CommandStatusMessage
    {
        get => commandStatusMessage;
        private set => SetField(ref commandStatusMessage, value);
    }

    public string DeviceName
    {
        get => deviceName;
        private set => SetField(ref deviceName, value);
    }

    public string DeviceSeatSummary
    {
        get => deviceSeatSummary;
        private set => SetField(ref deviceSeatSummary, value);
    }

    public string DeviceStateSummary
    {
        get => deviceStateSummary;
        private set => SetField(ref deviceStateSummary, value);
    }

    public string DeviceVersionSummary
    {
        get => deviceVersionSummary;
        private set => SetField(ref deviceVersionSummary, value);
    }

    public string DeviceCredentialSummary
    {
        get => deviceCredentialSummary;
        private set => SetField(ref deviceCredentialSummary, value);
    }

    public string DeviceInstalledAppsSummary
    {
        get => deviceInstalledAppsSummary;
        private set => SetField(ref deviceInstalledAppsSummary, value);
    }

    public string RecentCommandSummary
    {
        get => recentCommandSummary;
        private set => SetField(ref recentCommandSummary, value);
    }

    public string RotatedCredentialSecret
    {
        get => rotatedCredentialSecret;
        set => SetField(ref rotatedCredentialSecret, value);
    }

    public string CredentialStatus
    {
        get => credentialStatus;
        private set => SetField(ref credentialStatus, value);
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
                NotifyCommandStateChanged();
            }
        }
    }

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        OrganizationIdText = organizationId.ToString("D");
        BranchIdText = branchId.ToString("D");
    }

    public Task CreateEnrollmentCodeAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(OrganizationIdText, "OrganizationId", out var organizationId) ||
            !TryParseGuid(BranchIdText, "BranchId", out var branchId))
        {
            return Task.CompletedTask;
        }

        if (EnrollmentExpiresInSeconds <= 0)
        {
            ErrorMessage = "Enrollment lifetime must be positive.";
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var code = await apiClient.CreateEnrollmentCodeAsync(
                branchId,
                organizationId,
                EnrollmentExpiresInSeconds,
                cancellationToken);

            EnrollmentCode = code.Code;
            EnrollmentCodeExpiresAtUtc = code.ExpiresAtUtc;
            StatusMessage = $"Enrollment code {code.Code} created.";
        });
    }

    public Task DispatchDeviceCommandAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(DeviceIdText, "DeviceId", out var deviceId))
        {
            return Task.CompletedTask;
        }

        var type = CommandType.Trim();
        if (string.IsNullOrWhiteSpace(type))
        {
            ErrorMessage = "Command type is required.";
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var payload = string.IsNullOrWhiteSpace(CommandReason)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["reason"] = CommandReason.Trim() };
            var command = await apiClient.DispatchDeviceCommandAsync(deviceId, type, payload, cancellationToken);

            CommandIdText = command.CommandId.ToString("D");
            CommandStatus = "Pending";
            CommandStatusMessage = null;
            StatusMessage = $"Command {command.CommandId:D} dispatched.";
        });
    }

    public Task RefreshCommandStatusAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(DeviceIdText, "DeviceId", out var deviceId) ||
            !TryParseGuid(CommandIdText, "CommandId", out var commandId))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var status = await apiClient.GetDeviceCommandStatusAsync(deviceId, commandId, cancellationToken);

            CommandStatus = status.Status;
            CommandStatusMessage = status.Message;
            StatusMessage = $"Command {status.CommandId:D} status refreshed.";
        });
    }

    public Task RefreshDeviceDetailAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(DeviceIdText, "DeviceId", out var deviceId))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var detail = await apiClient.GetDeviceDetailAsync(deviceId, cancellationToken);

            DeviceName = detail.MachineName;
            DeviceSeatSummary = detail.SeatName is null
                ? "Unassigned"
                : $"{detail.SeatName} / {detail.ZoneName ?? "No zone"}";
            DeviceStateSummary = $"{(detail.IsOnline ? "Online" : "Offline")}, {(detail.IsLocked ? "locked" : "unlocked")}";
            DeviceVersionSummary = $"Agent {detail.AgentVersion} / Shell {detail.ShellVersion}";
            DeviceCredentialSummary = $"Active credentials: {detail.ActiveCredentialCount}";
            DeviceInstalledAppsSummary = $"Installed apps: {detail.InstalledAppCount}";
            var recentCommand = detail.RecentCommands.FirstOrDefault();
            RecentCommandSummary = recentCommand is null
                ? "Recent command: none"
                : $"{recentCommand.Type} {recentCommand.Status}";
            StatusMessage = $"Device {detail.MachineName} detail refreshed.";
        });
    }

    public Task RotateCredentialAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(DeviceIdText, "DeviceId", out var deviceId))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var rotated = await apiClient.RotateDeviceCredentialAsync(deviceId, cancellationToken);

            CredentialIdText = rotated.CredentialId.ToString("D");
            RotatedCredentialSecret = rotated.CredentialSecret;
            CredentialStatus = "Rotated";
            StatusMessage = $"Credential {rotated.CredentialId:D} rotated.";
        });
    }

    public Task RevokeCredentialAsync(CancellationToken cancellationToken)
    {
        ClearMessages();
        if (!TryParseGuid(DeviceIdText, "DeviceId", out var deviceId) ||
            !TryParseGuid(CredentialIdText, "CredentialId", out var credentialId))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var revoked = await apiClient.RevokeDeviceCredentialAsync(deviceId, credentialId, cancellationToken);

            CredentialStatus = "Revoked";
            RotatedCredentialSecret = string.Empty;
            StatusMessage = $"Credential {revoked.CredentialId:D} revoked.";
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

    private void NotifyCommandStateChanged()
    {
        CreateEnrollmentCodeCommand.NotifyCanExecuteChanged();
        DispatchDeviceCommandCommand.NotifyCanExecuteChanged();
        RefreshCommandStatusCommand.NotifyCanExecuteChanged();
        RefreshDeviceDetailCommand.NotifyCanExecuteChanged();
        RotateCredentialCommand.NotifyCanExecuteChanged();
        RevokeCredentialCommand.NotifyCanExecuteChanged();
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
