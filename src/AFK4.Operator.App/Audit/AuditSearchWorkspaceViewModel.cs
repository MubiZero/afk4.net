using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Audit;

namespace AFK4.Operator.App.Audit;

public sealed class AuditSearchWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IOperatorAuditApiClient apiClient;
    private string organizationIdText = "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08";
    private string branchIdText = "acfc0212-967f-4d84-94be-9003387b09c2";
    private string actionFilter = string.Empty;
    private string outcomeFilter = string.Empty;
    private string targetTypeFilter = string.Empty;
    private string fromUtcText = string.Empty;
    private string toUtcText = string.Empty;
    private string limitText = "50";
    private string summary = "Not loaded";
    private string statusMessage = string.Empty;
    private string? errorMessage;
    private bool isBusy;

    public AuditSearchWorkspaceViewModel(IOperatorAuditApiClient apiClient)
    {
        this.apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AuditRecordViewModel> Records { get; } = [];

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

    public string ActionFilter
    {
        get => actionFilter;
        set => SetField(ref actionFilter, value);
    }

    public string OutcomeFilter
    {
        get => outcomeFilter;
        set => SetField(ref outcomeFilter, value);
    }

    public string TargetTypeFilter
    {
        get => targetTypeFilter;
        set => SetField(ref targetTypeFilter, value);
    }

    public string FromUtcText
    {
        get => fromUtcText;
        set => SetField(ref fromUtcText, value);
    }

    public string ToUtcText
    {
        get => toUtcText;
        set => SetField(ref toUtcText, value);
    }

    public string LimitText
    {
        get => limitText;
        set => SetField(ref limitText, value);
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
        if (!TryParseGuid(BranchIdText, "BranchId", out var branchId) ||
            !TryParseLimit(out var limit) ||
            !TryParseDateTimeOffset(FromUtcText, "FromUtc", out var fromUtc) ||
            !TryParseDateTimeOffset(ToUtcText, "ToUtc", out var toUtc))
        {
            return Task.CompletedTask;
        }

        return RunApiCallAsync(async () =>
        {
            var result = await apiClient.SearchAsync(
                new OperatorAuditSearchRequest(
                    branchId,
                    Normalize(ActionFilter),
                    Normalize(OutcomeFilter),
                    Normalize(TargetTypeFilter),
                    fromUtc,
                    toUtc,
                    limit),
                cancellationToken);

            Records.Clear();
            foreach (var record in result.Records)
            {
                Records.Add(new AuditRecordViewModel(record));
            }

            Summary = result.Records.Count == 0
                ? "No audit records."
                : $"{result.Records.Count} audit record{(result.Records.Count == 1 ? string.Empty : "s")}, limit {result.Limit}.";
            StatusMessage = $"{result.Records.Count} audit record{(result.Records.Count == 1 ? string.Empty : "s")} loaded.";
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

    private bool TryParseLimit(out int? limit)
    {
        limit = null;
        if (string.IsNullOrWhiteSpace(LimitText))
        {
            return true;
        }

        if (int.TryParse(LimitText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed is >= 1 and <= 200)
        {
            limit = parsed;
            return true;
        }

        ErrorMessage = "Limit must be a number between 1 and 200.";
        return false;
    }

    private bool TryParseDateTimeOffset(string value, string fieldName, out DateTimeOffset? dateTimeOffset)
    {
        dateTimeOffset = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed))
        {
            dateTimeOffset = parsed;
            return true;
        }

        ErrorMessage = $"{fieldName} must be a valid date/time.";
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

    private static string? Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

public sealed class AuditRecordViewModel
{
    public AuditRecordViewModel(AuditRecordDto record)
    {
        AuditRecordId = record.AuditRecordId;
        Action = record.Action;
        Outcome = record.Outcome;
        TargetType = record.TargetType;
        TargetId = record.TargetId ?? string.Empty;
        TargetSummary = string.IsNullOrWhiteSpace(record.TargetId)
            ? record.TargetType
            : $"{record.TargetType} {record.TargetId}";
        ActorSummary = record.ActorStaffUserId?.ToString("D") ?? "System";
        SourceApp = record.SourceApp;
        DetailsJson = record.DetailsJson;
        CreatedAtUtc = record.CreatedAtUtc;
        CreatedAtText = record.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    }

    public Guid AuditRecordId { get; }

    public string Action { get; }

    public string Outcome { get; }

    public string TargetType { get; }

    public string TargetId { get; }

    public string TargetSummary { get; }

    public string ActorSummary { get; }

    public string SourceApp { get; }

    public string DetailsJson { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string CreatedAtText { get; }
}
