using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Operator.App.Shifts;

public sealed class ShiftWorkspaceViewModel : INotifyPropertyChanged
{
    private const string WaitingForBackendConfirmation = "Waiting for backend confirmation";
    private const string DefaultCurrencyCode = "USD";

    private readonly IOperatorShiftApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly AsyncRelayCommand loadCurrentShiftCommand;
    private readonly AsyncRelayCommand openShiftCommand;
    private readonly AsyncRelayCommand recordCashMovementCommand;
    private readonly AsyncRelayCommand closeShiftCommand;
    private Guid organizationId;
    private Guid branchId;
    private ShiftDto? currentShift;
    private long startingCashMinorUnits;
    private long countedCashMinorUnits;
    private long cashMovementAmountMinorUnits;
    private string openingNote = string.Empty;
    private string closingNote = string.Empty;
    private string cashMovementType = CashMovementTypeNames.CashIn;
    private string cashMovementReason = string.Empty;
    private bool isBusy;
    private string? pendingOperation;
    private string? statusMessage;
    private string? errorMessage;

    public ShiftWorkspaceViewModel(IOperatorShiftApiClient apiClient)
        : this(apiClient, new GuidIdempotencyKeyFactory())
    {
    }

    public ShiftWorkspaceViewModel(
        IOperatorShiftApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;

        loadCurrentShiftCommand = new AsyncRelayCommand(LoadCurrentShiftAsync, () => !IsBusy);
        openShiftCommand = new AsyncRelayCommand(OpenShiftAsync, () => !IsBusy && !CanRunMoneyWorkflows);
        recordCashMovementCommand = new AsyncRelayCommand(RecordCashMovementAsync, () => !IsBusy && CanRunMoneyWorkflows);
        closeShiftCommand = new AsyncRelayCommand(CloseShiftAsync, () => !IsBusy && CanRunMoneyWorkflows);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ShiftDto? CurrentShift
    {
        get => currentShift;
        private set
        {
            if (SetField(ref currentShift, value))
            {
                OnPropertyChanged(nameof(CurrentShiftId));
                OnPropertyChanged(nameof(CurrentShiftState));
                OnPropertyChanged(nameof(CanRunMoneyWorkflows));
                OnPropertyChanged(nameof(ExpectedCashMinorUnits));
                OnPropertyChanged(nameof(CountedCashResultMinorUnits));
                OnPropertyChanged(nameof(DifferenceMinorUnits));
                NotifyCommandStates();
            }
        }
    }

    public Guid? CurrentShiftId => CurrentShift?.ShiftId;

    public string? CurrentShiftState => CurrentShift?.State;

    public bool CanRunMoneyWorkflows => CurrentShift?.State == ShiftStateNames.Open;

    public long? ExpectedCashMinorUnits => CurrentShift?.ExpectedCash?.MinorUnits;

    public long? CountedCashResultMinorUnits => CurrentShift?.CountedCash?.MinorUnits;

    public long? DifferenceMinorUnits => CurrentShift?.Difference?.MinorUnits;

    public long StartingCashMinorUnits
    {
        get => startingCashMinorUnits;
        set => SetField(ref startingCashMinorUnits, value);
    }

    public string OpeningNote
    {
        get => openingNote;
        set => SetField(ref openingNote, value);
    }

    public long CountedCashMinorUnits
    {
        get => countedCashMinorUnits;
        set => SetField(ref countedCashMinorUnits, value);
    }

    public string ClosingNote
    {
        get => closingNote;
        set => SetField(ref closingNote, value);
    }

    public long CashMovementAmountMinorUnits
    {
        get => cashMovementAmountMinorUnits;
        set => SetField(ref cashMovementAmountMinorUnits, value);
    }

    public string CashMovementType
    {
        get => cashMovementType;
        set => SetField(ref cashMovementType, value);
    }

    public string CashMovementReason
    {
        get => cashMovementReason;
        set => SetField(ref cashMovementReason, value);
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

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public ICommand LoadCurrentShiftCommand => loadCurrentShiftCommand;

    public ICommand OpenShiftCommand => openShiftCommand;

    public ICommand RecordCashMovementCommand => recordCashMovementCommand;

    public ICommand CloseShiftCommand => closeShiftCommand;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        this.organizationId = organizationId;
        this.branchId = branchId;
    }

    public void SetCurrentShift(ShiftDto? shift)
    {
        CurrentShift = shift;
    }

    public async Task LoadCurrentShiftAsync(CancellationToken cancellationToken)
    {
        if (!TryGetBranchContext(out var branchIdValue))
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            CurrentShift = await apiClient.GetCurrentShiftAsync(branchIdValue, cancellationToken);
            StatusMessage = CurrentShift is null ? "No open shift." : "Open shift loaded.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = CreateUserFacingError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task OpenShiftAsync(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationContext(out var organizationIdValue) ||
            !TryGetBranchContext(out var branchIdValue) ||
            !TryValidateNonNegative(StartingCashMinorUnits, "Starting cash amount cannot be negative."))
        {
            return;
        }

        var request = new OpenShiftRequest(
            organizationIdValue,
            new MoneyDto(DefaultCurrencyCode, StartingCashMinorUnits),
            OpeningNote.Trim(),
            idempotencyKeyFactory.Create("shift-open"));

        await ExecuteBackendCommandAsync(
            async token =>
            {
                CurrentShift = await apiClient.OpenShiftAsync(branchIdValue, request, token);
                StatusMessage = "Shift opened.";
            },
            cancellationToken);
    }

    public async Task RecordCashMovementAsync(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationContext(out var organizationIdValue) ||
            !TryGetBranchContext(out var branchIdValue) ||
            !TryGetOpenShift(out var shiftId) ||
            !TryValidatePositive(CashMovementAmountMinorUnits, "Cash movement amount must be greater than zero.") ||
            !TryValidateCashMovementType() ||
            !TryValidateReason(CashMovementReason, "Cash movement reason is required."))
        {
            return;
        }

        var request = new RecordCashMovementRequest(
            organizationIdValue,
            CashMovementType.Trim(),
            new MoneyDto(CurrentShift?.StartingCash.CurrencyCode ?? DefaultCurrencyCode, CashMovementAmountMinorUnits),
            CashMovementReason.Trim(),
            idempotencyKeyFactory.Create("shift-cash-movement"));

        await ExecuteBackendCommandAsync(
            async token =>
            {
                await apiClient.RecordCashMovementAsync(shiftId, request, token);
                CurrentShift = await apiClient.GetCurrentShiftAsync(branchIdValue, token) ?? CurrentShift;
                StatusMessage = "Cash movement recorded.";
            },
            cancellationToken);
    }

    public async Task CloseShiftAsync(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationContext(out var organizationIdValue) ||
            !TryGetOpenShift(out var shiftId) ||
            !TryValidateNonNegative(CountedCashMinorUnits, "Counted cash amount cannot be negative."))
        {
            return;
        }

        var request = new CloseShiftRequest(
            organizationIdValue,
            new MoneyDto(CurrentShift?.StartingCash.CurrencyCode ?? DefaultCurrencyCode, CountedCashMinorUnits),
            ClosingNote.Trim(),
            idempotencyKeyFactory.Create("shift-close"));

        await ExecuteBackendCommandAsync(
            async token =>
            {
                CurrentShift = await apiClient.CloseShiftAsync(shiftId, request, token);
                StatusMessage = "Shift closed.";
            },
            cancellationToken);
    }

    private async Task ExecuteBackendCommandAsync(
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = WaitingForBackendConfirmation;
        IsBusy = true;

        try
        {
            await execute(cancellationToken);
            PendingOperation = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = CreateUserFacingError(exception);
            PendingOperation = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryGetOrganizationContext(out Guid organizationIdValue)
    {
        if (organizationId == Guid.Empty)
        {
            SetValidationError("Operator context is not loaded.");
            organizationIdValue = Guid.Empty;
            return false;
        }

        organizationIdValue = organizationId;
        return true;
    }

    private bool TryGetBranchContext(out Guid branchIdValue)
    {
        if (branchId == Guid.Empty)
        {
            SetValidationError("Operator context is not loaded.");
            branchIdValue = Guid.Empty;
            return false;
        }

        branchIdValue = branchId;
        return true;
    }

    private bool TryGetOpenShift(out Guid shiftId)
    {
        if (!CanRunMoneyWorkflows || CurrentShift is null)
        {
            SetValidationError("Open shift is required before money operations.");
            shiftId = Guid.Empty;
            return false;
        }

        shiftId = CurrentShift.ShiftId;
        return true;
    }

    private bool TryValidatePositive(long value, string message)
    {
        if (value <= 0)
        {
            SetValidationError(message);
            return false;
        }

        return true;
    }

    private bool TryValidateNonNegative(long value, string message)
    {
        if (value < 0)
        {
            SetValidationError(message);
            return false;
        }

        return true;
    }

    private bool TryValidateCashMovementType()
    {
        if (CashMovementType is CashMovementTypeNames.CashIn or CashMovementTypeNames.CashOut)
        {
            return true;
        }

        SetValidationError("Unsupported cash movement type.");
        return false;
    }

    private bool TryValidateReason(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SetValidationError(message);
            return false;
        }

        return true;
    }

    private void SetValidationError(string message)
    {
        ErrorMessage = message;
        StatusMessage = null;
        PendingOperation = null;
    }

    private static string CreateUserFacingError(Exception exception)
    {
        if (exception is HttpRequestException httpException)
        {
            return httpException.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                ? $"Permission denied: {httpException.Message}"
                : $"Network or API error: {httpException.Message}";
        }

        return exception.Message;
    }

    private void NotifyCommandStates()
    {
        loadCurrentShiftCommand.NotifyCanExecuteChanged();
        openShiftCommand.NotifyCanExecuteChanged();
        recordCashMovementCommand.NotifyCanExecuteChanged();
        closeShiftCommand.NotifyCanExecuteChanged();
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
