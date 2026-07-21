using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataWebhookEndpointTests
{
    // Фейковая фабрика/клиент: /status возвращает заданный статус — перепроверка перед кредитом.
    private sealed class StatusStubClient(string status) : IEskhataMerchantClient
    {
        public Task<EskhataCreateOrderResult> CreateOrderAsync(
            string invoiceId, long amountMinor, string currencyCode, string description,
            int merchantId, CancellationToken ct) => throw new NotSupportedException();

        public Task<string?> GetOrderStatusAsync(
            string invoiceId, string orderId, long amountMinor, string currencyCode, int posId,
            CancellationToken ct) => Task.FromResult<string?>(status);
    }

    private sealed class StatusStubFactory(string status) : IEskhataMerchantClientFactory
    {
        public Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid orgId, CancellationToken ct)
            => Task.FromResult<IEskhataMerchantClient?>(new StatusStubClient(status));
    }

    private static PlatformApiFactory CreateFactory(string status) =>
        new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<IEskhataMerchantClientFactory>();
            services.AddSingleton<IEskhataMerchantClientFactory>(new StatusStubFactory(status));
        });

    private static async Task<Guid> SeedEskhataIntentAsync(PlatformApiFactory factory, string state = "pending")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "EM Player",
            PhoneNumber = $"+99293111{playerId.ToString("N")[..4]}",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            AmountMinorUnits = 5_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "eskhata",
            GatewayPaymentId = "order-abc",
            GatewayPosId = 48741,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return intentId;
    }

    private static string CompletedBody(Guid intentId, string orderId = "order-abc") =>
        "{\"status\":true,\"code\":0,\"data\":{\"orderId\":\"" + orderId
        + "\",\"orderStatus\":\"COMPLETED\",\"invoiceId\":\"" + intentId.ToString("N")
        + "\",\"amount\":50.00,\"currency\":\"972\",\"posId\":48741}}";

    private static async Task<int> CountTopUpsAsync(PlatformApiFactory factory, Guid intentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerId = (await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId)).PlayerAccountId;
        return await db.LedgerEntries.CountAsync(e => e.PlayerAccountId == playerId && e.EntryType == "top_up");
    }

    private static Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string body) =>
        client.PostAsync("/api/public/payments/eskhata/webhook",
            new StringContent(body, Encoding.UTF8, "application/json"));

    [Fact]
    public async Task Webhook_Completed_VerifiedByStatus_CreditsOnce()
    {
        await using var factory = CreateFactory("COMPLETED");
        var intentId = await SeedEskhataIntentAsync(factory);
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, CompletedBody(intentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
        Assert.Equal("fulfilled", intent.State);
        Assert.NotNull(intent.FulfilledAtUtc);
    }

    [Fact]
    public async Task Webhook_Replay_DoesNotDoubleCredit()
    {
        await using var factory = CreateFactory("COMPLETED");
        var intentId = await SeedEskhataIntentAsync(factory);
        using var client = factory.CreateClient();
        var body = CompletedBody(intentId);

        var r1 = await PostWebhookAsync(client, body);
        var r2 = await PostWebhookAsync(client, body);

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_StatusNotCompleted_DoesNotCredit()
    {
        await using var factory = CreateFactory("NEW");
        var intentId = await SeedEskhataIntentAsync(factory);
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, CompletedBody(intentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // ack, но без кредита
        Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_OrderIdMismatch_DoesNotCredit()
    {
        await using var factory = CreateFactory("COMPLETED");
        var intentId = await SeedEskhataIntentAsync(factory);
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, CompletedBody(intentId, orderId: "WRONG"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_UnknownInvoice_Returns200Noop()
    {
        await using var factory = CreateFactory("COMPLETED");
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, CompletedBody(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_MalformedJson_ReturnsBadRequest()
    {
        await using var factory = CreateFactory("COMPLETED");
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, "{not-json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
