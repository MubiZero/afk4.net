using System.Net;
using System.Text;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

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

    private static async Task<Guid> SeedEskhataIntentAsync(
        PlatformApiFactory factory, string state = "pending", TimeSpan? age = null)
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
            CreatedAtUtc = DateTimeOffset.UtcNow - (age ?? TimeSpan.Zero)
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

    private static async Task<string> IntentStateAsync(PlatformApiFactory factory, Guid intentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return (await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId)).State;
    }

    /// <summary>
    /// Игрок отменил заявку, а потом всё-таки оплатил уже показанный QR. Деньги у клуба —
    /// кошелёк пополняется, заявка становится завершённой. Раньше вебхук отвечал банку «принято» и
    /// не зачислял ничего.
    /// </summary>
    [Fact]
    public async Task Webhook_PaidAfterPlayerCancelled_CreditsAndMarksFulfilled()
    {
        await using var factory = CreateFactory("COMPLETED");
        var intentId = await SeedEskhataIntentAsync(factory, state: "cancelled");
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, CompletedBody(intentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
        Assert.Equal("fulfilled", await IntentStateAsync(factory, intentId));
    }

    /// <summary>
    /// Процесс упал между зачислением и отметкой: деньги в журнале, заявка ещё «ожидает». Повтор
    /// вебхука дописывает отметку и не зачисляет второй раз.
    /// </summary>
    [Fact]
    public async Task Webhook_AfterCrashBetweenCreditAndMark_FinishesWithoutSecondCredit()
    {
        await using var factory = CreateFactory("COMPLETED");
        var intentId = await SeedEskhataIntentAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
            var credit = await scope.ServiceProvider.GetRequiredService<EfBillingCommandService>()
                .CreditOnlineTopUpAsync(intent.PlayerAccountId, intent.BranchId, new TopUpWalletRequest(
                    intent.OrganizationId,
                    new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                    EskhataSettlement.CreditReason,
                    intent.PaymentIntentId.ToString("N")), CancellationToken.None);
            Assert.True(credit.Succeeded);
        }
        using var client = factory.CreateClient();

        var response = await PostWebhookAsync(client, CompletedBody(intentId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
        Assert.Equal("fulfilled", await IntentStateAsync(factory, intentId));
    }

    private static Task ReconcileAsync(PlatformApiFactory factory) =>
        new EskhataReconciliationHostedService(
                factory.Services,
                factory.Services.GetRequiredService<TimeProvider>(),
                NullLogger<EskhataReconciliationHostedService>.Instance)
            .RunOnceAsync(CancellationToken.None);

    /// <summary>Вебхук не дошёл, игрок закрыл приложение — сверка находит оплату у банка сама.</summary>
    [Fact]
    public async Task Reconciliation_CreditsPaymentTheWebhookMissed()
    {
        await using var factory = CreateFactory("COMPLETED");
        var pending = await SeedEskhataIntentAsync(factory, age: TimeSpan.FromMinutes(10));
        var cancelled = await SeedEskhataIntentAsync(factory, state: "cancelled", age: TimeSpan.FromMinutes(10));

        await ReconcileAsync(factory);

        Assert.Equal(1, await CountTopUpsAsync(factory, pending));
        Assert.Equal("fulfilled", await IntentStateAsync(factory, pending));
        Assert.Equal(1, await CountTopUpsAsync(factory, cancelled));
        Assert.Equal("fulfilled", await IntentStateAsync(factory, cancelled));

        await ReconcileAsync(factory);
        Assert.Equal(1, await CountTopUpsAsync(factory, pending));
    }

    /// <summary>
    /// Неоплаченное, свежее (его ведёт опрос приложения) и старше суток — не трогаются.
    /// </summary>
    [Fact]
    public async Task Reconciliation_LeavesUnpaidFreshAndStaleIntentsAlone()
    {
        await using (var unpaidFactory = CreateFactory("NEW"))
        {
            var unpaid = await SeedEskhataIntentAsync(unpaidFactory, age: TimeSpan.FromMinutes(10));
            await ReconcileAsync(unpaidFactory);
            Assert.Equal(0, await CountTopUpsAsync(unpaidFactory, unpaid));
            Assert.Equal("pending", await IntentStateAsync(unpaidFactory, unpaid));
        }

        await using var factory = CreateFactory("COMPLETED");
        var fresh = await SeedEskhataIntentAsync(factory);
        var stale = await SeedEskhataIntentAsync(factory, age: TimeSpan.FromHours(25));

        await ReconcileAsync(factory);

        Assert.Equal(0, await CountTopUpsAsync(factory, fresh));
        Assert.Equal(0, await CountTopUpsAsync(factory, stale));
    }
}
