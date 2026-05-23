using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Pos;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Operator.App.Tests;

public sealed class PosWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ShiftId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SaleId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ReceiptId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task PayCashAsync_CreatesSaleThenManualPaymentAndExposesReceiptState()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("pos-sale-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SetCurrentShift(ShiftId);
        await viewModel.LoadCatalogAsync(CancellationToken.None);
        viewModel.AddProduct(apiClient.Catalog[0]);

        await viewModel.PayCashAsync(CancellationToken.None);

        Assert.Equal("pos-sale-001", apiClient.LastCreateSaleRequest?.IdempotencyKey);
        Assert.Equal(PaymentMethodNames.Cash, apiClient.LastManualPaymentRequest?.PaymentMethod);
        Assert.Equal("Оплачена", viewModel.LastSaleState);
        Assert.Equal(SaleId, viewModel.LastSaleId);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadCatalogAsync_ExposesOperatorFriendlyCatalogSummary()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("pos-sale-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.LoadCatalogAsync(CancellationToken.None);

        Assert.Equal("Товаров готово: 1", viewModel.CatalogSummary);
        Assert.True(viewModel.HasCatalog);
        var group = Assert.Single(viewModel.CatalogGroups);
        Assert.Equal("Товары", group.Label);
        Assert.Equal("12.00 USD", Assert.Single(group.Products).PriceText);
        Assert.Equal("Корзина пуста", viewModel.CartSummary);
        Assert.Equal("Откройте смену перед оплатой POS.", viewModel.CheckoutHint);
    }

    [Fact]
    public void AddProduct_MergesQuantitiesAndTracksCartTotal()
    {
        var viewModel = new PosWorkspaceViewModel(new RecordingPosApiClient(), new FixedIdempotencyKeyFactory("pos-sale-001"));
        var product = CreateProduct("Cola 0.5", stockOnHand: 24);

        viewModel.AddProduct(product);
        viewModel.AddProduct(product);

        Assert.Single(viewModel.CartLines);
        Assert.Equal(2, viewModel.CartLines[0].Quantity);
        Assert.Equal(2400, viewModel.CartTotalMinorUnits);
        Assert.Equal("2 товар(ов) / 24.00 USD", viewModel.CartSummary);
    }

    [Fact]
    public async Task PayCashAsync_RequiresOpenShiftBeforeBackendMutation()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("pos-sale-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.AddProduct(apiClient.Catalog[0]);

        await viewModel.PayCashAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.CreateSaleCallCount);
        Assert.Equal("Откройте смену перед оплатой POS.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task PayCardManualAsync_UsesCardManualProvider()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("pos-card-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SetCurrentShift(ShiftId);
        viewModel.AddProduct(apiClient.Catalog[0]);

        await viewModel.PayCardManualAsync(CancellationToken.None);

        Assert.Equal(PaymentMethodNames.CardManual, apiClient.LastManualPaymentRequest?.PaymentMethod);
        Assert.Equal("Оплачена", viewModel.LastSaleState);
    }

    [Fact]
    public async Task RefundLastSaleAsync_SendsReasonAndUpdatesState()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("refund-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SetCurrentShift(ShiftId);
        viewModel.AddProduct(apiClient.Catalog[0]);
        await viewModel.PayCashAsync(CancellationToken.None);
        viewModel.RefundReason = "customer returned unopened item";

        await viewModel.RefundLastSaleAsync(CancellationToken.None);

        Assert.Equal("customer returned unopened item", apiClient.LastRefundRequest?.Reason);
        Assert.Equal("Возврат", viewModel.LastSaleState);
    }

    [Fact]
    public async Task VoidDraftSaleAsync_CreatesDraftThenVoidsIt()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("void-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SetCurrentShift(ShiftId);
        viewModel.AddProduct(apiClient.Catalog[0]);
        viewModel.VoidReason = "mistaken draft";

        await viewModel.VoidDraftSaleAsync(CancellationToken.None);

        Assert.Equal("mistaken draft", apiClient.LastVoidRequest?.Reason);
        Assert.Equal("Отменена", viewModel.LastSaleState);
    }

    [Fact]
    public async Task LoadReceiptAsync_ReadsReceiptProjection()
    {
        var apiClient = new RecordingPosApiClient();
        var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("receipt-001"));

        await viewModel.LoadReceiptAsync(ReceiptId, CancellationToken.None);

        Assert.Equal("POS-20260514-0001", viewModel.LastReceipt?.ReceiptNumber);
        Assert.Equal(ReceiptId, viewModel.LastReceiptId);
    }

    private static PosProductDto CreateProduct(string name, int stockOnHand)
    {
        return new PosProductDto(
            ProductId,
            OrganizationId,
            BranchId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            name,
            "COLA-05",
            new MoneyDto("USD", 1200),
            TrackStock: true,
            AllowNegativeStock: false,
            IsActive: true,
            stockOnHand,
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"));
    }

    private static PosSaleDto CreateSale(string state)
    {
        return new PosSaleDto(
            SaleId,
            OrganizationId,
            BranchId,
            ShiftId,
            state,
            [new PosSaleLineDto(ProductId, "Cola 0.5", 1, new MoneyDto("USD", 1200), new MoneyDto("USD", 1200))],
            new MoneyDto("USD", 1200),
            Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            state == PosSaleStateNames.Paid ? DateTimeOffset.Parse("2026-05-14T09:01:00Z") : null,
            state == PosSaleStateNames.Refunded ? DateTimeOffset.Parse("2026-05-14T09:02:00Z") : null,
            state == PosSaleStateNames.Voided ? DateTimeOffset.Parse("2026-05-14T09:03:00Z") : null);
    }

    private sealed class FixedIdempotencyKeyFactory(string key) : IIdempotencyKeyFactory
    {
        public string Create(string operationName)
        {
            return key;
        }
    }

    private sealed class RecordingPosApiClient : IOperatorPosApiClient
    {
        public IReadOnlyList<PosProductDto> Catalog { get; } = [CreateProduct("Cola 0.5", stockOnHand: 24)];

        public int CreateSaleCallCount { get; private set; }

        public Guid LastCatalogBranchId { get; private set; }

        public Guid LastCreateSaleBranchId { get; private set; }

        public CreatePosSaleRequest? LastCreateSaleRequest { get; private set; }

        public ManualPaymentRequest? LastManualPaymentRequest { get; private set; }

        public RefundPosSaleRequest? LastRefundRequest { get; private set; }

        public VoidPosSaleRequest? LastVoidRequest { get; private set; }

        public Task<IReadOnlyList<PosProductDto>> GetCatalogAsync(Guid branchId, CancellationToken cancellationToken)
        {
            LastCatalogBranchId = branchId;
            return Task.FromResult(Catalog);
        }

        public Task<PosSaleDto> CreateSaleAsync(
            Guid branchId,
            CreatePosSaleRequest request,
            CancellationToken cancellationToken)
        {
            CreateSaleCallCount++;
            LastCreateSaleBranchId = branchId;
            LastCreateSaleRequest = request;
            return Task.FromResult(CreateSale(PosSaleStateNames.Draft));
        }

        public Task<PosSaleDto> PaySaleManualAsync(
            Guid saleId,
            ManualPaymentRequest request,
            CancellationToken cancellationToken)
        {
            LastManualPaymentRequest = request;
            return Task.FromResult(CreateSale(PosSaleStateNames.Paid));
        }

        public Task<PosSaleDto> RefundSaleAsync(
            Guid saleId,
            RefundPosSaleRequest request,
            CancellationToken cancellationToken)
        {
            LastRefundRequest = request;
            return Task.FromResult(CreateSale(PosSaleStateNames.Refunded));
        }

        public Task<PosSaleDto> VoidSaleAsync(
            Guid saleId,
            VoidPosSaleRequest request,
            CancellationToken cancellationToken)
        {
            LastVoidRequest = request;
            return Task.FromResult(CreateSale(PosSaleStateNames.Voided));
        }

        public Task<PosSaleDto> GetSaleAsync(Guid saleId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateSale(PosSaleStateNames.Paid));
        }

        public Task<ReceiptDto> GetReceiptAsync(Guid receiptId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReceiptDto(
                receiptId,
                OrganizationId,
                BranchId,
                SaleId,
                "POS-20260514-0001",
                "sale",
                new MoneyDto("USD", 1200),
                DateTimeOffset.Parse("2026-05-14T09:01:00Z")));
        }
    }
}
