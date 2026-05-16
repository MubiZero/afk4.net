using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Operator.App.Shifts;

public sealed class ShiftWorkspaceViewModel : INotifyPropertyChanged
{
    private const string WaitingForBackendConfirmation = "Waiting for backend confirmation";
    private const string LoadingReports = "Loading reports";
    private const string ExportingReportCsv = "Exporting report CSV";
    private const string DefaultCurrencyCode = "USD";

    private readonly IOperatorShiftApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly IReportCsvFileWriter reportCsvFileWriter;
    private readonly AsyncRelayCommand loadCurrentShiftCommand;
    private readonly AsyncRelayCommand openShiftCommand;
    private readonly AsyncRelayCommand recordCashMovementCommand;
    private readonly AsyncRelayCommand closeShiftCommand;
    private readonly AsyncRelayCommand loadReportsCommand;
    private readonly AsyncRelayCommand exportShiftReportCsvCommand;
    private readonly AsyncRelayCommand exportSalesReportCsvCommand;
    private readonly AsyncRelayCommand exportGameplayTimeReportCsvCommand;
    private readonly AsyncRelayCommand exportCashOperationReportCsvCommand;
    private readonly AsyncRelayCommand exportOperatorActionReportCsvCommand;
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
    private string reportFromUtcText = string.Empty;
    private string reportToUtcText = string.Empty;
    private string reportLimitText = "50";
    private string shiftReportSummary = "No shift report loaded.";
    private string salesReportSummary = "No sales report loaded.";
    private string gameplayTimeReportSummary = "No gameplay time report loaded.";
    private string cashOperationReportSummary = "No cash operation report loaded.";
    private string operatorActionReportSummary = "No operator action report loaded.";
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
        : this(apiClient, idempotencyKeyFactory, new UnconfiguredReportCsvFileWriter())
    {
    }

    public ShiftWorkspaceViewModel(
        IOperatorShiftApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory,
        IReportCsvFileWriter reportCsvFileWriter)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;
        this.reportCsvFileWriter = reportCsvFileWriter;

        loadCurrentShiftCommand = new AsyncRelayCommand(LoadCurrentShiftAsync, () => !IsBusy);
        openShiftCommand = new AsyncRelayCommand(OpenShiftAsync, () => !IsBusy && !CanRunMoneyWorkflows);
        recordCashMovementCommand = new AsyncRelayCommand(RecordCashMovementAsync, () => !IsBusy && CanRunMoneyWorkflows);
        closeShiftCommand = new AsyncRelayCommand(CloseShiftAsync, () => !IsBusy && CanRunMoneyWorkflows);
        loadReportsCommand = new AsyncRelayCommand(LoadReportsAsync, () => !IsBusy);
        exportShiftReportCsvCommand = new AsyncRelayCommand(ExportShiftReportCsvAsync, () => !IsBusy);
        exportSalesReportCsvCommand = new AsyncRelayCommand(ExportSalesReportCsvAsync, () => !IsBusy);
        exportGameplayTimeReportCsvCommand = new AsyncRelayCommand(ExportGameplayTimeReportCsvAsync, () => !IsBusy);
        exportCashOperationReportCsvCommand = new AsyncRelayCommand(ExportCashOperationReportCsvAsync, () => !IsBusy);
        exportOperatorActionReportCsvCommand = new AsyncRelayCommand(ExportOperatorActionReportCsvAsync, () => !IsBusy);
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

    public ObservableCollection<ShiftReportRowViewModel> ShiftReportRows { get; } = [];

    public ObservableCollection<SalesReportRowViewModel> SalesReportRows { get; } = [];

    public ObservableCollection<GameplayTimeReportRowViewModel> GameplayTimeReportRows { get; } = [];

    public ObservableCollection<CashOperationReportRowViewModel> CashOperationReportRows { get; } = [];

    public ObservableCollection<OperatorActionReportRowViewModel> OperatorActionReportRows { get; } = [];

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

    public string ReportFromUtcText
    {
        get => reportFromUtcText;
        set => SetField(ref reportFromUtcText, value);
    }

    public string ReportToUtcText
    {
        get => reportToUtcText;
        set => SetField(ref reportToUtcText, value);
    }

    public string ReportLimitText
    {
        get => reportLimitText;
        set => SetField(ref reportLimitText, value);
    }

    public string ShiftReportSummary
    {
        get => shiftReportSummary;
        private set => SetField(ref shiftReportSummary, value);
    }

    public string SalesReportSummary
    {
        get => salesReportSummary;
        private set => SetField(ref salesReportSummary, value);
    }

    public string GameplayTimeReportSummary
    {
        get => gameplayTimeReportSummary;
        private set => SetField(ref gameplayTimeReportSummary, value);
    }

    public string CashOperationReportSummary
    {
        get => cashOperationReportSummary;
        private set => SetField(ref cashOperationReportSummary, value);
    }

    public string OperatorActionReportSummary
    {
        get => operatorActionReportSummary;
        private set => SetField(ref operatorActionReportSummary, value);
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

    public ICommand LoadReportsCommand => loadReportsCommand;

    public ICommand ExportShiftReportCsvCommand => exportShiftReportCsvCommand;

    public ICommand ExportSalesReportCsvCommand => exportSalesReportCsvCommand;

    public ICommand ExportGameplayTimeReportCsvCommand => exportGameplayTimeReportCsvCommand;

    public ICommand ExportCashOperationReportCsvCommand => exportCashOperationReportCsvCommand;

    public ICommand ExportOperatorActionReportCsvCommand => exportOperatorActionReportCsvCommand;

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

    public async Task LoadReportsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetBranchContext(out var branchIdValue) ||
            !TryParseReportFilters(out var fromUtc, out var toUtc, out var limit))
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = LoadingReports;
        IsBusy = true;

        try
        {
            var shiftReport = await apiClient.GetShiftReportAsync(branchIdValue, fromUtc, toUtc, limit, cancellationToken);
            var salesReport = await apiClient.GetSalesReportAsync(branchIdValue, fromUtc, toUtc, limit, cancellationToken);
            var gameplayTimeReport = await apiClient.GetGameplayTimeReportAsync(branchIdValue, fromUtc, toUtc, limit, cancellationToken);
            var cashOperationReport = await apiClient.GetCashOperationReportAsync(branchIdValue, fromUtc, toUtc, limit, cancellationToken);
            var operatorActionReport = await apiClient.GetOperatorActionReportAsync(branchIdValue, fromUtc, toUtc, limit, cancellationToken);

            ShiftReportRows.Clear();
            foreach (var row in shiftReport.Rows)
            {
                ShiftReportRows.Add(new ShiftReportRowViewModel(row));
            }

            SalesReportRows.Clear();
            foreach (var row in salesReport.Rows)
            {
                SalesReportRows.Add(new SalesReportRowViewModel(row));
            }

            GameplayTimeReportRows.Clear();
            foreach (var row in gameplayTimeReport.Rows)
            {
                GameplayTimeReportRows.Add(new GameplayTimeReportRowViewModel(row));
            }

            CashOperationReportRows.Clear();
            foreach (var row in cashOperationReport.Rows)
            {
                CashOperationReportRows.Add(new CashOperationReportRowViewModel(row));
            }

            OperatorActionReportRows.Clear();
            foreach (var row in operatorActionReport.Rows)
            {
                OperatorActionReportRows.Add(new OperatorActionReportRowViewModel(row));
            }

            ShiftReportSummary = $"{shiftReport.Rows.Count} shifts loaded.";
            SalesReportSummary = $"Gross {FormatMoney(salesReport.GrossSalesTotal)}, refunds {FormatMoney(salesReport.RefundsTotal)}, net {FormatMoney(salesReport.NetSalesTotal)}.";
            GameplayTimeReportSummary = $"Gameplay {gameplayTimeReport.TotalDurationSeconds} seconds, package {gameplayTimeReport.TotalPackageSeconds}, bonus {gameplayTimeReport.TotalBonusSeconds}, revenue {FormatMoney(gameplayTimeReport.GameplayRevenueTotal)}.";
            CashOperationReportSummary = $"Cash in {FormatMoney(cashOperationReport.CashInTotal)}, cash out {FormatMoney(cashOperationReport.CashOutTotal)}, net {FormatMoney(cashOperationReport.NetCashTotal)}.";
            OperatorActionReportSummary = $"{operatorActionReport.TotalActionCount} operator actions across {operatorActionReport.Rows.Count} groups.";
            StatusMessage = "Reports loaded.";
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

    public Task ExportShiftReportCsvAsync(CancellationToken cancellationToken)
    {
        return ExportReportCsvAsync(
            "afk4-shifts-report.csv",
            apiClient.ExportShiftReportCsvAsync,
            cancellationToken);
    }

    public Task ExportSalesReportCsvAsync(CancellationToken cancellationToken)
    {
        return ExportReportCsvAsync(
            "afk4-sales-report.csv",
            apiClient.ExportSalesReportCsvAsync,
            cancellationToken);
    }

    public Task ExportGameplayTimeReportCsvAsync(CancellationToken cancellationToken)
    {
        return ExportReportCsvAsync(
            "afk4-gameplay-time-report.csv",
            apiClient.ExportGameplayTimeReportCsvAsync,
            cancellationToken);
    }

    public Task ExportCashOperationReportCsvAsync(CancellationToken cancellationToken)
    {
        return ExportReportCsvAsync(
            "afk4-cash-operations-report.csv",
            apiClient.ExportCashOperationReportCsvAsync,
            cancellationToken);
    }

    public Task ExportOperatorActionReportCsvAsync(CancellationToken cancellationToken)
    {
        return ExportReportCsvAsync(
            "afk4-operator-actions-report.csv",
            apiClient.ExportOperatorActionReportCsvAsync,
            cancellationToken);
    }

    private async Task ExportReportCsvAsync(
        string suggestedFileName,
        Func<Guid, DateTimeOffset?, DateTimeOffset?, int?, CancellationToken, Task<string>> exportAsync,
        CancellationToken cancellationToken)
    {
        if (!TryGetBranchContext(out var branchIdValue) ||
            !TryParseReportFilters(out var fromUtc, out var toUtc, out var limit))
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = ExportingReportCsv;
        IsBusy = true;

        try
        {
            var csv = await exportAsync(branchIdValue, fromUtc, toUtc, limit, cancellationToken);
            var savedPath = await reportCsvFileWriter.SaveAsync(suggestedFileName, csv, cancellationToken);
            StatusMessage = savedPath is null
                ? "CSV export cancelled."
                : $"CSV exported to {savedPath}.";
            PendingOperation = null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            HttpRequestException or
            IOException or
            UnauthorizedAccessException)
        {
            ErrorMessage = CreateUserFacingError(exception);
            PendingOperation = null;
        }
        finally
        {
            IsBusy = false;
        }
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

    private bool TryParseReportFilters(
        out DateTimeOffset? fromUtc,
        out DateTimeOffset? toUtc,
        out int? limit)
    {
        fromUtc = null;
        toUtc = null;
        limit = null;

        if (!TryParseOptionalDateTime(ReportFromUtcText, "Report from date/time is invalid.", out fromUtc) ||
            !TryParseOptionalDateTime(ReportToUtcText, "Report to date/time is invalid.", out toUtc) ||
            !TryParseOptionalPositiveInteger(ReportLimitText, "Report limit must be a positive whole number.", out limit))
        {
            return false;
        }

        return true;
    }

    private bool TryParseOptionalDateTime(
        string value,
        string message,
        out DateTimeOffset? parsedValue)
    {
        parsedValue = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            parsedValue = parsed;
            return true;
        }

        SetValidationError(message);
        return false;
    }

    private bool TryParseOptionalPositiveInteger(
        string value,
        string message,
        out int? parsedValue)
    {
        parsedValue = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            parsedValue = parsed;
            return true;
        }

        SetValidationError(message);
        return false;
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
        loadReportsCommand.NotifyCanExecuteChanged();
        exportShiftReportCsvCommand.NotifyCanExecuteChanged();
        exportSalesReportCsvCommand.NotifyCanExecuteChanged();
        exportGameplayTimeReportCsvCommand.NotifyCanExecuteChanged();
        exportCashOperationReportCsvCommand.NotifyCanExecuteChanged();
        exportOperatorActionReportCsvCommand.NotifyCanExecuteChanged();
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

    private static string FormatMoney(MoneyDto money)
    {
        return $"{money.CurrencyCode} {money.MinorUnits}";
    }
}

public sealed class ShiftReportRowViewModel(ShiftReportRowDto row)
{
    public string ShiftId => row.ShiftId.ToString("N")[..8];

    public string State => row.State;

    public string OpenedAtUtc => FormatDateTime(row.OpenedAtUtc);

    public string ExpectedCash => FormatMoney(row.ExpectedCash);

    public string CountedCash => row.CountedCash is null ? "" : FormatMoney(row.CountedCash);

    public string Difference => row.Difference is null ? "" : FormatMoney(row.Difference);

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(MoneyDto money)
    {
        return $"{money.CurrencyCode} {money.MinorUnits}";
    }
}

public sealed class SalesReportRowViewModel(SalesReportRowDto row)
{
    public string PosSaleId => row.PosSaleId.ToString("N")[..8];

    public string State => row.State;

    public string CreatedAtUtc => FormatDateTime(row.CreatedAtUtc);

    public string Total => FormatMoney(row.Total);

    public string PaidAmount => FormatMoney(row.PaidAmount);

    public string RefundAmount => FormatMoney(row.RefundAmount);

    public int ItemQuantity => row.ItemQuantity;

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(MoneyDto money)
    {
        return $"{money.CurrencyCode} {money.MinorUnits}";
    }
}

public sealed class GameplayTimeReportRowViewModel(GameplayTimeReportRowDto row)
{
    public string SessionId => row.SessionId.ToString("N")[..8];

    public string State => row.State;

    public string StartedAtUtc => row.StartedAtUtc is null ? "" : FormatDateTime(row.StartedAtUtc.Value);

    public int DurationSeconds => row.DurationSeconds;

    public int PackageSeconds => row.PackageSeconds;

    public int BonusSeconds => row.BonusSeconds;

    public string GameplayRevenue => FormatMoney(row.GameplayRevenue);

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(MoneyDto money)
    {
        return $"{money.CurrencyCode} {money.MinorUnits}";
    }
}

public sealed class CashOperationReportRowViewModel(CashOperationReportRowDto row)
{
    public string OperationId => row.OperationId.ToString("N")[..8];

    public string SourceType => row.SourceType;

    public string OperationType => row.OperationType;

    public string CashImpact => FormatMoney(row.CashImpact);

    public string CreatedAtUtc => row.CreatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public string Reason => row.Reason;

    private static string FormatMoney(MoneyDto money)
    {
        return $"{money.CurrencyCode} {money.MinorUnits}";
    }
}

public sealed class OperatorActionReportRowViewModel(OperatorActionReportRowDto row)
{
    public string ActorDisplayName => row.ActorDisplayName;

    public string Action => row.Action;

    public string Outcome => row.Outcome;

    public int Count => row.Count;

    public string LastAtUtc => row.LastAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
