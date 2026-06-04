using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Payments.DcGate;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class DcGateClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => this.responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private static DcGateClient CreateClient(StubHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://dcgate.example") },
            apiKey: "test-api-key");

    private static HttpResponseMessage OkPayment() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "paymentId": "pay_xyz",
                  "status": "pending",
                  "amount": "50.00",
                  "currency": "TJS",
                  "comment": "AFK4-COMMENT-0001",
                  "expiresAt": "2026-06-03T12:30:00Z",
                  "payUrl": "http://pay.dc.tj/?A=1&s=50.00&c=cmt"
                }
                """,
                System.Text.Encoding.UTF8,
                "application/json")
        };

    [Fact]
    public async Task CreatePaymentAsync_SendsBearerAndMajorUnitAmount()
    {
        var handler = new StubHandler(_ => OkPayment());
        var client = CreateClient(handler);

        var result = await client.CreatePaymentAsync(
            amountMinorUnits: 5_000,
            currencyCode: "TJS",
            externalOrderId: "abcd1234",
            metadata: new { playerAccountId = "p1", branchId = "b1" },
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/payments", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-api-key", handler.LastRequest.Headers.Authorization.Parameter);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("50.00", sent.RootElement.GetProperty("amount").GetString());
        Assert.Equal("abcd1234", sent.RootElement.GetProperty("externalOrderId").GetString());
        Assert.True(sent.RootElement.TryGetProperty("metadata", out _));

        Assert.Equal("pay_xyz", result.PaymentId);
        Assert.Equal("AFK4-COMMENT-0001", result.Comment);
        Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", result.PayUrl);
        Assert.NotNull(result.ExpiresAt);
    }

    [Theory]
    [InlineData(5_000, "50.00")]
    [InlineData(99, "0.99")]
    [InlineData(1, "0.01")]
    [InlineData(100_000, "1000.00")]
    public async Task CreatePaymentAsync_FormatsMinorUnitsAsMajorString(long minor, string expected)
    {
        var handler = new StubHandler(_ => OkPayment());
        var client = CreateClient(handler);

        await client.CreatePaymentAsync(minor, "TJS", "ord", metadata: new { }, CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(expected, sent.RootElement.GetProperty("amount").GetString());
    }

    [Fact]
    public async Task CreatePaymentAsync_ThrowsOnNonSuccessStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CreatePaymentAsync(5_000, "TJS", "ord", metadata: new { }, CancellationToken.None));
    }
}
