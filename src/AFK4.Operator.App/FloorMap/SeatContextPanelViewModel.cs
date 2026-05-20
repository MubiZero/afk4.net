using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Operator.App.FloorMap;

public sealed class SeatContextPanelViewModel : INotifyPropertyChanged
{
    private const string WaitingForBackendConfirmation = "Waiting for backend confirmation";
    private static readonly IReadOnlyList<BillingModeOptionViewModel> BillingModes =
    [
        new("Guest / no ledger", "", "Fast smoke start; no player account or ledger entry."),
        new("Postpaid debt", BillingModeNames.PostpaidDebt, "Requires player account and tariff version."),
        new("Prepaid wallet", BillingModeNames.PrepaidWallet, "Requires player account, tariff version, and wallet balance."),
        new("Package", BillingModeNames.Package, "Requires player account and player package.")
    ];

    private readonly IOperatorSessionApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly Func<CancellationToken, Task>? refreshAfterSuccess;
    private readonly AsyncRelayCommand startGuestSessionCommand;
    private readonly AsyncRelayCommand extendSessionCommand;
    private readonly AsyncRelayCommand transferSessionCommand;
    private readonly AsyncRelayCommand endSessionCommand;
    private Guid? organizationId;
    private Guid? branchId;
    private FloorMapSeatViewModel? selectedSeat;
    private string playerAccountIdText = "";
    private int durationMinutes = 60;
    private int additionalMinutes = 15;
    private string billingMode = "";
    private string tariffRuleVersionId = "manual-v1";
    private string tariffVersionIdText = "";
    private string playerPackageIdText = "";
    private string targetSeatIdText = "";
    private string reason = "operator request";
    private string? errorMessage;
    private string? pendingOperation;
    private string? statusMessage;
    private bool isBusy;

    public SeatContextPanelViewModel(
        IOperatorSessionApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory,
        Func<CancellationToken, Task>? refreshAfterSuccess = null)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;
        this.refreshAfterSuccess = refreshAfterSuccess;

        startGuestSessionCommand = new AsyncRelayCommand(StartGuestSessionAsync, () => !IsBusy && CanStartGuestSession);
        extendSessionCommand = new AsyncRelayCommand(ExtendSessionAsync, () => !IsBusy && HasActiveSession);
        transferSessionCommand = new AsyncRelayCommand(TransferSessionAsync, () => !IsBusy && HasActiveSession);
        endSessionCommand = new AsyncRelayCommand(EndSessionAsync, () => !IsBusy && HasActiveSession);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FloorMapSeatViewModel? SelectedSeat
    {
        get => selectedSeat;
        private set
        {
            if (SetField(ref selectedSeat, value))
            {
                OnPropertyChanged(nameof(HasActiveSession));
                OnPropertyChanged(nameof(CanStartGuestSession));
                OnPropertyChanged(nameof(SelectedSeatSummary));
                NotifyCommandStates();
            }
        }
    }

    public bool HasActiveSession => SelectedSeat?.ActiveSessionId is not null;

    public bool CanStartGuestSession => SelectedSeat is not null && !HasActiveSession;

    public string SelectedSeatSummary => SelectedSeat is null
        ? "Select a seat from the floor."
        : $"{SelectedSeat.Name} - {SelectedSeat.State}";

    public IReadOnlyList<BillingModeOptionViewModel> BillingModeOptions => BillingModes;

    public bool IsPlayerBillingRequired => !string.IsNullOrWhiteSpace(BillingMode);

    public string PlayerAccountIdText
    {
        get => playerAccountIdText;
        set => SetField(ref playerAccountIdText, value);
    }

    public int DurationMinutes
    {
        get => durationMinutes;
        set => SetField(ref durationMinutes, value);
    }

    public int AdditionalMinutes
    {
        get => additionalMinutes;
        set => SetField(ref additionalMinutes, value);
    }

    public string BillingMode
    {
        get => billingMode;
        set
        {
            if (SetField(ref billingMode, value))
            {
                OnPropertyChanged(nameof(IsPlayerBillingRequired));
            }
        }
    }

    public string TariffRuleVersionId
    {
        get => tariffRuleVersionId;
        set => SetField(ref tariffRuleVersionId, value);
    }

    public string TariffVersionIdText
    {
        get => tariffVersionIdText;
        set => SetField(ref tariffVersionIdText, value);
    }

    public string PlayerPackageIdText
    {
        get => playerPackageIdText;
        set => SetField(ref playerPackageIdText, value);
    }

    public string TargetSeatIdText
    {
        get => targetSeatIdText;
        set => SetField(ref targetSeatIdText, value);
    }

    public string Reason
    {
        get => reason;
        set => SetField(ref reason, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public string? PendingOperation
    {
        get => pendingOperation;
        private set => SetField(ref pendingOperation, value);
    }

    public string? StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public ICommand StartGuestSessionCommand => startGuestSessionCommand;

    public ICommand ExtendSessionCommand => extendSessionCommand;

    public ICommand TransferSessionCommand => transferSessionCommand;

    public ICommand EndSessionCommand => endSessionCommand;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        this.organizationId = organizationId;
        this.branchId = branchId;
    }

    public void SelectSeat(FloorMapSeatViewModel? seat)
    {
        SelectedSeat = seat;
        ErrorMessage = null;
        PendingOperation = null;
        StatusMessage = null;
    }

    public async Task StartGuestSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSelectedSeat(out var seat) ||
            !TryGetOperatorContext(out var organizationIdValue, out var branchIdValue) ||
            !TryValidatePositive(DurationMinutes, "Duration must be greater than zero.") ||
            !TryValidateTariffRuleVersion() ||
            !TryParseOptionalGuid(PlayerAccountIdText, "Player account id", out var playerAccountId) ||
            !TryParseOptionalGuid(TariffVersionIdText, "Tariff version id", out var tariffVersionId) ||
            !TryParseOptionalGuid(PlayerPackageIdText, "Player package id", out var playerPackageId))
        {
            return;
        }

        if (seat.ActiveSessionId is not null)
        {
            SetValidationError("Selected seat already has an active session.");
            return;
        }

        if (IsPlayerBillingRequired && playerAccountId is null)
        {
            SetValidationError("Choose Guest / no ledger for a fast guest start, or enter a player account id for billed modes.");
            return;
        }

        var request = new StartGuestSessionRequest(
            organizationIdValue,
            seat.SeatId,
            DurationMinutes,
            TariffRuleVersionId.Trim(),
            idempotencyKeyFactory.Create("session-start"),
            playerAccountId,
                BillingMode.Trim(),
            tariffVersionId,
            playerPackageId);

        await ExecuteBackendCommandAsync(
            token => apiClient.StartGuestSessionAsync(branchIdValue, request, token),
            cancellationToken);
    }

    public async Task ExtendSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId) ||
            !TryValidatePositive(AdditionalMinutes, "Additional minutes must be greater than zero.") ||
            !TryValidateTariffRuleVersion() ||
            !TryParseOptionalGuid(PlayerAccountIdText, "Player account id", out var playerAccountId) ||
            !TryParseOptionalGuid(TariffVersionIdText, "Tariff version id", out var tariffVersionId) ||
            !TryParseOptionalGuid(PlayerPackageIdText, "Player package id", out var playerPackageId))
        {
            return;
        }

        var request = new ExtendSessionRequest(
            AdditionalMinutes,
            TariffRuleVersionId.Trim(),
            idempotencyKeyFactory.Create("session-extend"),
            playerAccountId,
            BillingMode,
            tariffVersionId,
            playerPackageId);

        await ExecuteBackendCommandAsync(
            token => apiClient.ExtendSessionAsync(sessionId, request, token),
            cancellationToken);
    }

    public async Task TransferSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId))
        {
            return;
        }

        if (!Guid.TryParse(TargetSeatIdText, out var targetSeatId))
        {
            SetValidationError("Target seat id is required.");
            return;
        }

        var request = new TransferSessionRequest(
            targetSeatId,
            idempotencyKeyFactory.Create("session-transfer"));

        await ExecuteBackendCommandAsync(
            token => apiClient.TransferSessionAsync(sessionId, request, token),
            cancellationToken);
    }

    public async Task EndSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId))
        {
            return;
        }

        var request = new EndSessionRequest(
            string.IsNullOrWhiteSpace(Reason) ? "operator request" : Reason.Trim(),
            idempotencyKeyFactory.Create("session-end"));

        await ExecuteBackendCommandAsync(
            token => apiClient.EndSessionAsync(sessionId, request, token),
            cancellationToken);
    }

    private async Task ExecuteBackendCommandAsync(
        Func<CancellationToken, Task<SessionCommandResponse>> execute,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = WaitingForBackendConfirmation;
        IsBusy = true;

        try
        {
            await execute(cancellationToken);

            if (refreshAfterSuccess is not null)
            {
                await refreshAfterSuccess(cancellationToken);
            }

            PendingOperation = WaitingForBackendConfirmation;
            StatusMessage = "Session command accepted.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = exception.Message;
            PendingOperation = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryGetSelectedSeat(out FloorMapSeatViewModel seat)
    {
        if (SelectedSeat is null)
        {
            SetValidationError("Select a seat first.");
            seat = null!;
            return false;
        }

        seat = SelectedSeat;
        return true;
    }

    private bool TryGetActiveSession(out Guid sessionId)
    {
        if (SelectedSeat?.ActiveSessionId is null)
        {
            SetValidationError("Selected seat has no active session.");
            sessionId = Guid.Empty;
            return false;
        }

        sessionId = SelectedSeat.ActiveSessionId.Value;
        return true;
    }

    private bool TryGetOperatorContext(out Guid organizationIdValue, out Guid branchIdValue)
    {
        if (organizationId is null || branchId is null)
        {
            SetValidationError("Operator context is not loaded.");
            organizationIdValue = Guid.Empty;
            branchIdValue = Guid.Empty;
            return false;
        }

        organizationIdValue = organizationId.Value;
        branchIdValue = branchId.Value;
        return true;
    }

    private bool TryValidatePositive(int value, string message)
    {
        if (value <= 0)
        {
            SetValidationError(message);
            return false;
        }

        return true;
    }

    private bool TryValidateTariffRuleVersion()
    {
        if (string.IsNullOrWhiteSpace(TariffRuleVersionId))
        {
            SetValidationError("Tariff rule version is required.");
            return false;
        }

        return true;
    }

    private bool TryParseOptionalGuid(string text, string fieldName, out Guid? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (Guid.TryParse(text, out var parsed))
        {
            value = parsed;
            return true;
        }

        SetValidationError($"{fieldName} must be a valid GUID.");
        value = null;
        return false;
    }

    private void SetValidationError(string message)
    {
        ErrorMessage = message;
        StatusMessage = null;
        PendingOperation = null;
    }

    private void NotifyCommandStates()
    {
        startGuestSessionCommand.NotifyCanExecuteChanged();
        extendSessionCommand.NotifyCanExecuteChanged();
        transferSessionCommand.NotifyCanExecuteChanged();
        endSessionCommand.NotifyCanExecuteChanged();
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

public sealed record BillingModeOptionViewModel(
    string Label,
    string Value,
    string Description);
