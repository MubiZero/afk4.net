using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Pos;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorPosApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ShiftId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SaleId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ReceiptId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task GetCatalogAsync_GetsBearerAuthenticatedCatalog()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse<IReadOnlyList<PosProductDto>>([CreateProduct()]));
        var client = CreateClient(handler);

        var catalog = await client.GetCatalogAsync(BranchId, CancellationToken.None);

        Assert.Single(catalog);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/pos/catalog", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Fact]
    public async Task CreateSaleAsync_PostsSaleToBranch()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateSale(PosSaleStateNames.Draft)));
        var client = CreateClient(handler);
        var request = new CreatePosSaleRequest(
            OrganizationId,
            ShiftId,
            [new PosSaleLineDto(ProductId, "Cola 0.5", 2, new MoneyDto("USD", 1200), new MoneyDto("USD", 2400))],
            "pos-sale-001");

        var sale = await client.CreateSaleAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(SaleId, sale.PosSaleId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/pos/sales", handler.LastPathAndQuery);

        var body = DeserializeRequest<CreatePosSaleRequest>(handler.LastRequestBody);
        Assert.Equal("pos-sale-001", body.IdempotencyKey);
        Assert.Equal(2, body.Lines[0].Quantity);
    }

    [Fact]
    public async Task PaySaleManualAsync_PostsManualPayment()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateSale(PosSaleStateNames.Paid)));
        var client = CreateClient(handler);
        var request = new ManualPaymentRequest(
            OrganizationId,
            PaymentMethodNames.Cash,
            new MoneyDto("USD", 2400),
            "cash drawer",
            "pay-001");

        var sale = await client.PaySaleManualAsync(SaleId, request, CancellationToken.None);

        Assert.Equal(PosSaleStateNames.Paid, sale.State);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/pos/sales/{SaleId:D}/payments/manual", handler.LastPathAndQuery);

        var body = DeserializeRequest<ManualPaymentRequest>(handler.LastRequestBody);
        Assert.Equal(PaymentMethodNames.Cash, body.PaymentMethod);
        Assert.Equal("pay-001", body.IdempotencyKey);
    }

    [Fact]
    public async Task RefundAndVoid_PostToSaleActionEndpoints()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonContentResponse(CreateSale(PosSaleStateNames.Refunded)),
            JsonContentResponse(CreateSale(PosSaleStateNames.Voided))
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var refunded = await client.RefundSaleAsync(
            SaleId,
            new RefundPosSaleRequest(OrganizationId, "customer return", "refund-001"),
            CancellationToken.None);
        var refundPath = handler.LastPathAndQuery;

        var voided = await client.VoidSaleAsync(
            SaleId,
            new VoidPosSaleRequest(OrganizationId, "mistaken draft", "void-001"),
            CancellationToken.None);

        Assert.Equal(PosSaleStateNames.Refunded, refunded.State);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/pos/sales/{SaleId:D}/refunds", refundPath);
        Assert.Equal(PosSaleStateNames.Voided, voided.State);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/pos/sales/{SaleId:D}/void", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task GetSaleAndReceipt_GetReadEndpoints()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonContentResponse(CreateSale(PosSaleStateNames.Paid)),
            JsonContentResponse(new ReceiptDto(
                ReceiptId,
                OrganizationId,
                BranchId,
                SaleId,
                "POS-20260514-0001",
                "sale",
                new MoneyDto("USD", 2400),
                DateTimeOffset.Parse("2026-05-14T09:01:00Z")))
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var sale = await client.GetSaleAsync(SaleId, CancellationToken.None);
        var salePath = handler.LastPathAndQuery;
        var receipt = await client.GetReceiptAsync(ReceiptId, CancellationToken.None);

        Assert.Equal(PosSaleStateNames.Paid, sale.State);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/pos/sales/{SaleId:D}", salePath);
        Assert.Equal("POS-20260514-0001", receipt.ReceiptNumber);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/receipts/{ReceiptId:D}", handler.LastPathAndQuery);
    }

    private static HttpOperatorPosApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        return new HttpOperatorPosApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
    }

    private static PosProductDto CreateProduct()
    {
        return new PosProductDto(
            ProductId,
            OrganizationId,
            BranchId,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "Cola 0.5",
            "COLA-05",
            new MoneyDto("USD", 1200),
            TrackStock: true,
            AllowNegativeStock: false,
            IsActive: true,
            StockOnHand: 24,
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
            [new PosSaleLineDto(ProductId, "Cola 0.5", 2, new MoneyDto("USD", 1200), new MoneyDto("USD", 2400))],
            new MoneyDto("USD", 2400),
            Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            state == PosSaleStateNames.Paid ? DateTimeOffset.Parse("2026-05-14T09:01:00Z") : null,
            state == PosSaleStateNames.Refunded ? DateTimeOffset.Parse("2026-05-14T09:02:00Z") : null,
            state == PosSaleStateNames.Voided ? DateTimeOffset.Parse("2026-05-14T09:03:00Z") : null);
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonContentResponse<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public string? LastPathAndQuery { get; private set; }

        public string? LastRequestBody { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }

    private sealed class StaticOperatorTokenStore : IOperatorTokenStore
    {
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
                Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                OrganizationId,
                "Cashier One",
                "staff-access-token",
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                "refresh-token",
                DateTimeOffset.Parse("2026-05-15T10:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
