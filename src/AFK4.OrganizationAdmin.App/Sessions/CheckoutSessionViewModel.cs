using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.OrganizationAdmin.App.Mvvm;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.OrganizationAdmin.App.Sessions;

/// <summary>
/// Drives the unified-checkout dialog: it loads the read-only quote (time
/// charge + attached POS = grand total), lets the operator settle it with one
/// or more split-payment parts (cash / card / deposit), and submits the
/// checkout. Mirrors the web operator's checkout flow.
/// </summary>
public sealed class CheckoutSessionViewModel : INotifyPropertyChanged
{
    private const string DefaultCurrencyCode = "TJS";

    private readonly IOperatorSessionApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly Guid organizationId;
    private readonly Guid sessionId;
    private readonly Func<CancellationToken, Task>? onCompleted;
    private readonly Action? onClosed;
    private readonly AsyncRelayCommand confirmCommand;
    private readonly AsyncRelayCommand reloadCommand;
    private readonly RelayCommand addPaymentCommand;
    private readonly RelayCommand removePaymentCommand;
    private readonly RelayCommand cancelCommand;
    private readonly RelayCommand suggestWalletCommand;

    private string currencyCode = DefaultCurrencyCode;
    private long timeChargeMinorUnits;
    private long posTotalMinorUnits;
    private long grandTotalMinorUnits;
    private int billableSeconds;
    private long? walletBalanceMinorUnits;
    private bool isLoading = true;
    private bool isBusy;
    private bool isCompleted;
    private string? errorMessage;
    private string? validationMessage;

    public CheckoutSessionViewModel(
        IOperatorSessionApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory,
        Guid organizationId,
        Guid sessionId,
        string? currencyCode = null,
        Func<CancellationToken, Task>? onCompleted = null,
        Action? onClosed = null)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;
        this.organizationId = organizationId;
        this.sessionId = sessionId;
        this.onCompleted = onCompleted;
        this.onClosed = onClosed;
        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            this.currencyCode = currencyCode.Trim().ToUpperInvariant();
        }

        Payments = new ObservableCollection<CheckoutPaymentRowViewModel>();
        confirmCommand = new AsyncRelayCommand(ConfirmAsync, () => CanConfirm);
        reloadCommand = new AsyncRelayCommand(LoadQuoteAsync, () => !IsBusy);
        addPaymentCommand = new RelayCommand(_ => AddPaymentRow(PaymentMethodNames.Cash, ""), _ => !IsBusy && !IsCompleted);
        removePaymentCommand = new RelayCommand(RemovePaymentRow, _ => !IsBusy && !IsCompleted && Payments.Count > 1);
        cancelCommand = new RelayCommand(_ => onClosed?.Invoke(), _ => !IsBusy);
        suggestWalletCommand = new RelayCommand(_ => SuggestWallet(), _ => !IsBusy && !IsCompleted && (walletBalanceMinorUnits ?? 0) > 0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CheckoutPaymentRowViewModel> Payments { get; }

    public ICommand ConfirmCommand => confirmCommand;

    public ICommand ReloadCommand => reloadCommand;

    public ICommand AddPaymentCommand => addPaymentCommand;

    public ICommand RemovePaymentCommand => removePaymentCommand;

    public ICommand CancelCommand => cancelCommand;

    public ICommand SuggestWalletCommand => suggestWalletCommand;

    public string CurrencyCode => currencyCode;

    public string TimeChargeText => $"{FormatMoney(timeChargeMinorUnits)} {currencyCode}";

    public string PosTotalText => $"{FormatMoney(posTotalMinorUnits)} {currencyCode}";

    public string GrandTotalText => $"{FormatMoney(grandTotalMinorUnits)} {currencyCode}";

    public string BilledDurationText => FormatBilledDuration(billableSeconds);

    public bool HasWallet => walletBalanceMinorUnits is not null;

    public string WalletBalanceText => walletBalanceMinorUnits is { } balance
        ? $"Депозит: {FormatMoney(balance)} {currencyCode}"
        : "";

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetField(ref isLoading, value))
            {
                NotifyCommandStates();
            }
        }
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

    public bool IsCompleted
    {
        get => isCompleted;
        private set
        {
            if (SetField(ref isCompleted, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public string? ValidationMessage
    {
        get => validationMessage;
        private set => SetField(ref validationMessage, value);
    }

    public long PaidMinorUnits => Payments.Sum(row => row.AmountMinorUnits ?? 0);

    public long RemainingMinorUnits => grandTotalMinorUnits - PaidMinorUnits;

    public bool CanConfirm
    {
        get
        {
            if (IsBusy || IsLoading || IsCompleted)
            {
                return false;
            }

            var walletTotal = Payments.Where(row => row.Method == PaymentMethodNames.Wallet).Sum(row => row.AmountMinorUnits ?? 0);
            if (walletTotal > (walletBalanceMinorUnits ?? 0))
            {
                return false;
            }

            if (Payments.Any(row => row.HasInvalidAmount))
            {
                return false;
            }

            return grandTotalMinorUnits == 0 ? PaidMinorUnits == 0 : PaidMinorUnits == grandTotalMinorUnits;
        }
    }

    public async Task LoadQuoteAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var quote = await apiClient.GetCheckoutQuoteAsync(sessionId, cancellationToken);
            currencyCode = string.IsNullOrWhiteSpace(quote.GrandTotal.CurrencyCode)
                ? currencyCode
                : quote.GrandTotal.CurrencyCode.ToUpperInvariant();
            timeChargeMinorUnits = quote.TimeCharge.MinorUnits;
            posTotalMinorUnits = quote.PosTotal.MinorUnits;
            grandTotalMinorUnits = quote.GrandTotal.MinorUnits;
            billableSeconds = quote.BillableSeconds;
            walletBalanceMinorUnits = quote.WalletBalance?.MinorUnits;

            Payments.Clear();
            AddPaymentRow(PaymentMethodNames.Cash, grandTotalMinorUnits > 0 ? FormatMoney(grandTotalMinorUnits) : "");

            NotifyTotals();
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RecomputeValidation();
        }
    }

    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (!CanConfirm)
        {
            return;
        }

        var parts = Payments
            .Where(row => (row.AmountMinorUnits ?? 0) > 0)
            .Select(row => new PaymentPartDto(row.Method, new MoneyDto(currencyCode, row.AmountMinorUnits!.Value)))
            .ToList();

        var request = new SessionCheckoutRequest(
            organizationId,
            parts,
            idempotencyKeyFactory.Create("session-checkout"));

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await apiClient.CheckoutSessionAsync(sessionId, request, cancellationToken);
            IsCompleted = true;
            if (onCompleted is not null)
            {
                await onCompleted(cancellationToken);
            }

            onClosed?.Invoke();
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

    private void AddPaymentRow(string method, string amountText)
    {
        var row = new CheckoutPaymentRowViewModel(method, amountText);
        row.PropertyChanged += OnPaymentRowChanged;
        Payments.Add(row);
        NotifyCommandStates();
        RecomputeValidation();
    }

    private void RemovePaymentRow(object? parameter)
    {
        if (parameter is not CheckoutPaymentRowViewModel row || Payments.Count <= 1)
        {
            return;
        }

        row.PropertyChanged -= OnPaymentRowChanged;
        Payments.Remove(row);
        NotifyCommandStates();
        RecomputeValidation();
    }

    private void SuggestWallet()
    {
        if (walletBalanceMinorUnits is not { } balance)
        {
            return;
        }

        foreach (var existing in Payments)
        {
            existing.PropertyChanged -= OnPaymentRowChanged;
        }

        Payments.Clear();

        var fromWallet = Math.Min(balance, grandTotalMinorUnits);
        AddPaymentRow(PaymentMethodNames.Wallet, FormatMoney(fromWallet));
        var remainder = grandTotalMinorUnits - fromWallet;
        if (remainder > 0)
        {
            AddPaymentRow(PaymentMethodNames.Cash, FormatMoney(remainder));
        }
    }

    private void OnPaymentRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        RecomputeValidation();
    }

    private void RecomputeValidation()
    {
        OnPropertyChanged(nameof(PaidMinorUnits));
        OnPropertyChanged(nameof(RemainingMinorUnits));
        OnPropertyChanged(nameof(CanConfirm));
        confirmCommand.NotifyCanExecuteChanged();

        var walletTotal = Payments.Where(row => row.Method == PaymentMethodNames.Wallet).Sum(row => row.AmountMinorUnits ?? 0);
        if (Payments.Any(row => row.HasInvalidAmount))
        {
            ValidationMessage = "Проверьте сумму платежа.";
        }
        else if (walletTotal > (walletBalanceMinorUnits ?? 0))
        {
            ValidationMessage = "Оплата с депозита превышает баланс.";
        }
        else if (grandTotalMinorUnits != 0 && PaidMinorUnits != grandTotalMinorUnits)
        {
            ValidationMessage = RemainingMinorUnits > 0
                ? $"Не хватает {FormatMoney(RemainingMinorUnits)} {currencyCode}"
                : $"Превышение на {FormatMoney(-RemainingMinorUnits)} {currencyCode}";
        }
        else
        {
            ValidationMessage = null;
        }
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(CurrencyCode));
        OnPropertyChanged(nameof(TimeChargeText));
        OnPropertyChanged(nameof(PosTotalText));
        OnPropertyChanged(nameof(GrandTotalText));
        OnPropertyChanged(nameof(BilledDurationText));
        OnPropertyChanged(nameof(HasWallet));
        OnPropertyChanged(nameof(WalletBalanceText));
    }

    private void NotifyCommandStates()
    {
        confirmCommand.NotifyCanExecuteChanged();
        reloadCommand.NotifyCanExecuteChanged();
        addPaymentCommand.NotifyCanExecuteChanged();
        removePaymentCommand.NotifyCanExecuteChanged();
        cancelCommand.NotifyCanExecuteChanged();
        suggestWalletCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanConfirm));
    }

    internal static string FormatMoney(long minorUnits)
    {
        return (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    internal static string FormatBilledDuration(int billableSeconds)
    {
        var totalMinutes = Math.Max(0, (int)Math.Round(billableSeconds / 60d));
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours == 0 ? $"{minutes} мин" : $"{hours} ч {minutes:00} мин";
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

/// <summary>One editable split-payment row (method + amount) in the checkout dialog.</summary>
public sealed class CheckoutPaymentRowViewModel : INotifyPropertyChanged
{
    public static readonly IReadOnlyList<CheckoutMethodOption> MethodOptions =
    [
        new(PaymentMethodNames.Cash, "Наличные"),
        new(PaymentMethodNames.CardManual, "Карта"),
        new(PaymentMethodNames.Wallet, "Депозит")
    ];

    private string method;
    private string amountText;

    public CheckoutPaymentRowViewModel(string method, string amountText)
    {
        this.method = method;
        this.amountText = amountText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CheckoutMethodOption> Methods => MethodOptions;

    public string Method
    {
        get => method;
        set
        {
            if (!string.Equals(method, value, StringComparison.Ordinal))
            {
                method = value;
                OnPropertyChanged();
            }
        }
    }

    public string AmountText
    {
        get => amountText;
        set
        {
            if (!string.Equals(amountText, value, StringComparison.Ordinal))
            {
                amountText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AmountMinorUnits));
                OnPropertyChanged(nameof(HasInvalidAmount));
            }
        }
    }

    /// <summary>Parsed minor units, or null when the field is blank or malformed.</summary>
    public long? AmountMinorUnits => ParseAmount(amountText);

    /// <summary>True only when there is text that does not parse to a positive amount.</summary>
    public bool HasInvalidAmount => !string.IsNullOrWhiteSpace(amountText) && AmountMinorUnits is null;

    internal static long? ParseAmount(string value)
    {
        var normalized = value?.Trim().Replace(',', '.') ?? "";
        if (normalized.Length == 0)
        {
            return null;
        }

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var major))
        {
            return null;
        }

        if (major <= 0m || decimal.Round(major, 2) != major)
        {
            return null;
        }

        return (long)decimal.Round(major * 100m);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record CheckoutMethodOption(string Value, string Label);
