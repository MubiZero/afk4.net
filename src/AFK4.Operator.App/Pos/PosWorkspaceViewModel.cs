using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Operator.App.Pos;

public sealed class PosWorkspaceViewModel : INotifyPropertyChanged
{
    private const string WaitingForBackendConfirmation = "Waiting for backend confirmation";
    private const string DefaultCurrencyCode = "USD";

    private readonly IOperatorPosApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly AsyncRelayCommand refreshCatalogCommand;
    private readonly AsyncRelayCommand payCashCommand;
    private readonly AsyncRelayCommand payCardManualCommand;
    private readonly AsyncRelayCommand refundLastSaleCommand;
    private readonly AsyncRelayCommand voidDraftSaleCommand;
    private readonly RelayCommand addProductCommand;
    private Guid organizationId;
    private Guid branchId;
    private Guid? currentShiftId;
    private PosSaleDto? lastSale;
    private ReceiptDto? lastReceipt;
    private string paymentNote = "manual POS payment";
    private string refundReason = "customer return";
    private string voidReason = "mistaken draft";
    private bool isBusy;
    private string? pendingOperation;
    private string? statusMessage;
    private string? errorMessage;

    public PosWorkspaceViewModel(IOperatorPosApiClient apiClient)
        : this(apiClient, new GuidIdempotencyKeyFactory())
    {
    }

    public PosWorkspaceViewModel(
        IOperatorPosApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;

        Catalog = [];
        CatalogGroups = [];
        CartLines = [];
        addProductCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is PosProductDto product)
                {
                    AddProduct(product);
                }
            },
            parameter => !IsBusy && parameter is PosProductDto);
        refreshCatalogCommand = new AsyncRelayCommand(LoadCatalogAsync, () => !IsBusy);
        payCashCommand = new AsyncRelayCommand(PayCashAsync, () => CanRunCartCommand());
        payCardManualCommand = new AsyncRelayCommand(PayCardManualAsync, () => CanRunCartCommand());
        refundLastSaleCommand = new AsyncRelayCommand(RefundLastSaleAsync, () => CanRunLastSaleCommand(PosSaleStateNames.Paid));
        voidDraftSaleCommand = new AsyncRelayCommand(VoidDraftSaleAsync, () => CanRunCartCommand());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PosProductDto> Catalog { get; }

    public ObservableCollection<PosCatalogGroupViewModel> CatalogGroups { get; }

    public ObservableCollection<PosCartLineViewModel> CartLines { get; }

    public Guid? CurrentShiftId
    {
        get => currentShiftId;
        private set
        {
            if (SetField(ref currentShiftId, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public PosSaleDto? LastSale
    {
        get => lastSale;
        private set
        {
            if (SetField(ref lastSale, value))
            {
                OnPropertyChanged(nameof(LastSaleId));
                OnPropertyChanged(nameof(LastSaleState));
                NotifyCommandStates();
            }
        }
    }

    public Guid? LastSaleId => LastSale?.PosSaleId;

    public string? LastSaleState => LastSale is null ? null : ToDisplayState(LastSale.State);

    public ReceiptDto? LastReceipt
    {
        get => lastReceipt;
        private set
        {
            if (SetField(ref lastReceipt, value))
            {
                OnPropertyChanged(nameof(LastReceiptId));
            }
        }
    }

    public Guid? LastReceiptId => LastReceipt?.ReceiptId;

    public string PaymentNote
    {
        get => paymentNote;
        set => SetField(ref paymentNote, value);
    }

    public string RefundReason
    {
        get => refundReason;
        set => SetField(ref refundReason, value);
    }

    public string VoidReason
    {
        get => voidReason;
        set => SetField(ref voidReason, value);
    }

    public long CartTotalMinorUnits => CartLines.Sum(line => line.LineTotalMinorUnits);

    public int CartItemCount => CartLines.Sum(line => line.Quantity);

    public string CartSummary => $"{CartItemCount} item(s) / {FormatMoney(CartTotalMinorUnits)} {CartCurrencyCode}";

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

    public ICommand RefreshCatalogCommand => refreshCatalogCommand;

    public ICommand AddProductCommand => addProductCommand;

    public ICommand PayCashCommand => payCashCommand;

    public ICommand PayCardManualCommand => payCardManualCommand;

    public ICommand RefundLastSaleCommand => refundLastSaleCommand;

    public ICommand VoidDraftSaleCommand => voidDraftSaleCommand;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        this.organizationId = organizationId;
        this.branchId = branchId;
    }

    public void SetCurrentShift(Guid? shiftId)
    {
        CurrentShiftId = shiftId;
    }

    public async Task LoadCatalogAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var products = await apiClient.GetCatalogAsync(branchId, cancellationToken);
            Catalog.Clear();
            foreach (var product in products.OrderBy(product => product.CategoryId).ThenBy(product => product.Name))
            {
                Catalog.Add(product);
            }

            RebuildCatalogGroups();
            StatusMessage = $"{Catalog.Count} POS product(s) loaded.";
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

    public void AddProduct(PosProductDto product)
    {
        var existing = CartLines.SingleOrDefault(line => line.ProductId == product.ProductId);
        if (existing is null)
        {
            CartLines.Add(new PosCartLineViewModel(product, quantity: 1));
        }
        else
        {
            existing.Increment();
        }

        LastSale = null;
        LastReceipt = null;
        ErrorMessage = null;
        NotifyCartChanged();
    }

    public Task PayCashAsync(CancellationToken cancellationToken)
    {
        return PayAsync(PaymentMethodNames.Cash, cancellationToken);
    }

    public Task PayCardManualAsync(CancellationToken cancellationToken)
    {
        return PayAsync(PaymentMethodNames.CardManual, cancellationToken);
    }

    public async Task RefundLastSaleAsync(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationContext(out var organizationIdValue) ||
            !TryGetLastSaleId(out var saleId) ||
            !TryValidateReason(RefundReason, "Refund reason is required."))
        {
            return;
        }

        var request = new RefundPosSaleRequest(
            organizationIdValue,
            RefundReason.Trim(),
            idempotencyKeyFactory.Create("pos-refund"));

        await ExecuteBackendCommandAsync(
            async token =>
            {
                LastSale = await apiClient.RefundSaleAsync(saleId, request, token);
                StatusMessage = "POS sale refunded.";
            },
            cancellationToken);
    }

    public async Task VoidDraftSaleAsync(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationContext(out var organizationIdValue) ||
            !TryGetBranchContext(out var branchIdValue) ||
            !TryGetOpenShift(out var shiftId) ||
            !TryValidateCart() ||
            !TryValidateReason(VoidReason, "Void reason is required."))
        {
            return;
        }

        await ExecuteBackendCommandAsync(
            async token =>
            {
                var draft = await apiClient.CreateSaleAsync(
                    branchIdValue,
                    CreateSaleRequest(organizationIdValue, shiftId, idempotencyKeyFactory.Create("pos-sale")),
                    token);

                var voidRequest = new VoidPosSaleRequest(
                    organizationIdValue,
                    VoidReason.Trim(),
                    idempotencyKeyFactory.Create("pos-void"));
                LastSale = await apiClient.VoidSaleAsync(draft.PosSaleId, voidRequest, token);
                StatusMessage = "POS sale voided.";
            },
            cancellationToken);
    }

    public async Task LoadReceiptAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            LastReceipt = await apiClient.GetReceiptAsync(receiptId, cancellationToken);
            StatusMessage = $"Receipt {LastReceipt.ReceiptNumber} loaded.";
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

    private async Task PayAsync(string paymentMethod, CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationContext(out var organizationIdValue) ||
            !TryGetBranchContext(out var branchIdValue) ||
            !TryGetOpenShift(out var shiftId) ||
            !TryValidateCart())
        {
            return;
        }

        await ExecuteBackendCommandAsync(
            async token =>
            {
                var draft = await apiClient.CreateSaleAsync(
                    branchIdValue,
                    CreateSaleRequest(organizationIdValue, shiftId, idempotencyKeyFactory.Create("pos-sale")),
                    token);

                var payment = new ManualPaymentRequest(
                    organizationIdValue,
                    paymentMethod,
                    draft.Total,
                    string.IsNullOrWhiteSpace(PaymentNote) ? "manual POS payment" : PaymentNote.Trim(),
                    idempotencyKeyFactory.Create("pos-payment"));

                LastSale = await apiClient.PaySaleManualAsync(draft.PosSaleId, payment, token);
                CartLines.Clear();
                NotifyCartChanged();
                StatusMessage = paymentMethod == PaymentMethodNames.Cash
                    ? "Cash POS payment confirmed."
                    : "Manual card POS payment confirmed.";
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

    private CreatePosSaleRequest CreateSaleRequest(
        Guid organizationIdValue,
        Guid shiftId,
        string idempotencyKey)
    {
        return new CreatePosSaleRequest(
            organizationIdValue,
            shiftId,
            CartLines.Select(line => line.ToSaleLine()).ToList(),
            idempotencyKey);
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
        if (CurrentShiftId is null)
        {
            SetValidationError("Open shift is required before POS payment.");
            shiftId = Guid.Empty;
            return false;
        }

        shiftId = CurrentShiftId.Value;
        return true;
    }

    private bool TryGetLastSaleId(out Guid saleId)
    {
        if (LastSale?.PosSaleId is null)
        {
            SetValidationError("No POS sale is selected.");
            saleId = Guid.Empty;
            return false;
        }

        saleId = LastSale.PosSaleId;
        return true;
    }

    private bool TryValidateCart()
    {
        if (CartLines.Count == 0)
        {
            SetValidationError("Cart is empty.");
            return false;
        }

        return true;
    }

    private bool TryValidateReason(string reason, string message)
    {
        if (string.IsNullOrWhiteSpace(reason))
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

    private void RebuildCatalogGroups()
    {
        CatalogGroups.Clear();
        foreach (var group in Catalog.GroupBy(product => product.CategoryId).OrderBy(group => group.Key))
        {
            CatalogGroups.Add(new PosCatalogGroupViewModel(group.Key, group.ToList()));
        }
    }

    private string CartCurrencyCode =>
        CartLines.FirstOrDefault()?.UnitPrice.CurrencyCode ?? Catalog.FirstOrDefault()?.Price.CurrencyCode ?? DefaultCurrencyCode;

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

    private static string ToDisplayState(string state)
    {
        return state switch
        {
            PosSaleStateNames.Draft => "Draft",
            PosSaleStateNames.PendingPayment => "Pending Payment",
            PosSaleStateNames.Paid => "Paid",
            PosSaleStateNames.Refunded => "Refunded",
            PosSaleStateNames.Voided => "Voided",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(state.Replace('_', ' '))
        };
    }

    private static string FormatMoney(long minorUnits)
    {
        return (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private bool CanRunCartCommand()
    {
        return !IsBusy && CartLines.Count > 0;
    }

    private bool CanRunLastSaleCommand(string state)
    {
        return !IsBusy && LastSale?.State == state;
    }

    private void NotifyCartChanged()
    {
        OnPropertyChanged(nameof(CartTotalMinorUnits));
        OnPropertyChanged(nameof(CartItemCount));
        OnPropertyChanged(nameof(CartSummary));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        refreshCatalogCommand.NotifyCanExecuteChanged();
        addProductCommand.NotifyCanExecuteChanged();
        payCashCommand.NotifyCanExecuteChanged();
        payCardManualCommand.NotifyCanExecuteChanged();
        refundLastSaleCommand.NotifyCanExecuteChanged();
        voidDraftSaleCommand.NotifyCanExecuteChanged();
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

public sealed class PosCatalogGroupViewModel
{
    public PosCatalogGroupViewModel(Guid categoryId, IReadOnlyList<PosProductDto> products)
    {
        CategoryId = categoryId;
        Products = products;
    }

    public Guid CategoryId { get; }

    public string Label => $"Category {CategoryId.ToString("D")[..8]}";

    public IReadOnlyList<PosProductDto> Products { get; }
}
