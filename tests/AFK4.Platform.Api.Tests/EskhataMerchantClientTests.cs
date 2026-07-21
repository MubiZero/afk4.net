using System.Net;
using System.Text.Json;
using AFK4.Platform.Api.Payments.Eskhata;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataMerchantClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
        public string? LastBody { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> r) => responder = r;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastRequest = req;
            LastBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            return responder(req);
        }
    }

    private static EskhataMerchantClient Create(StubHandler h) =>
        new(new HttpClient(h) { BaseAddress = new Uri("https://em.example") },
            companyId: "918b6ea7-59fd-4e49-9481-9f2ca6a32b75",
            hashKey: "4f7fbbf60e8e4a3194042c55f474b40b");

    private static HttpResponseMessage OkCreate() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""
        {"status":true,"code":0,"message":"Успешно","data":{
          "posId":48741,"orderStatus":"NEW","invoiceId":"inv1",
          "orderId":"3818cdcccc6b4e8f8ff93bdc048a74e1",
          "qr":"0002010102...","invoiceUrl":"https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH"}}
        """, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task CreateOrderAsync_SendsSignedType3Body_AndParsesQrAndUrl()
    {
        var handler = new StubHandler(_ => OkCreate());
        var client = Create(handler);

        var result = await client.CreateOrderAsync(
            invoiceId: "inv1", amountMinor: 5000, currencyCode: "972",
            description: "AFK4 wallet top-up", merchantId: 28652, CancellationToken.None);

        Assert.Equal("/merchant/api/v1/orders/create", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.True(handler.LastRequest.Headers.Contains("X-CompanyId"));
        using var sent = JsonDocument.Parse(handler.LastBody!);
        var root = sent.RootElement;
        Assert.Equal(3, root.GetProperty("orderTypeId").GetInt32());
        Assert.Equal(28652, root.GetProperty("merchantId").GetInt32());
        Assert.False(root.TryGetProperty("posId", out _)); // тип 3: posId не шлём
        Assert.False(string.IsNullOrEmpty(root.GetProperty("hash").GetString()));

        Assert.Equal("3818cdcccc6b4e8f8ff93bdc048a74e1", result.OrderId);
        Assert.Equal("NEW", result.OrderStatus);
        Assert.False(string.IsNullOrEmpty(result.Qr));
        Assert.Contains("/invoices/hlR2oH", result.InvoiceUrl);
        Assert.Equal(48741, result.PosId);
    }

    [Fact]
    public async Task GetOrderStatusAsync_ReturnsOrderStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"status":true,"code":0,"data":{"orderId":"o1","orderStatus":"COMPLETED","invoiceId":"inv1","posId":48741}}
            """, System.Text.Encoding.UTF8, "application/json")
        });
        var status = await Create(handler).GetOrderStatusAsync("inv1", "o1", 5000, "972", 48741, CancellationToken.None);
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsWhenBankStatusFalse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":false,"code":-2,"message":"неверные параметры запроса","data":{}}""",
                System.Text.Encoding.UTF8, "application/json")
        });
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Create(handler).CreateOrderAsync("inv1", 5000, "972", "d", 28652, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrderStatusAsync_ReturnsNull_OnMalformedBody()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", System.Text.Encoding.UTF8, "application/json")
        });
        var status = await Create(handler).GetOrderStatusAsync("inv1", "o1", 5000, "972", 48741, CancellationToken.None);
        Assert.Null(status);
    }
}
