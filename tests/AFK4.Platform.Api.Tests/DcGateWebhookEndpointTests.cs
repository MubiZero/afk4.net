using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class DcGateWebhookEndpointTests
{
    private const string WebhookSecret = "test-webhook-secret";

    private static PlatformApiFactory CreateFactory() => new PlatformApiFactory();

    private static async Task SeedGatewayAsync(PlatformApiFactory factory, string projectId, string webhookSecret)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Security.ISecretProtector>();
        await SeedGatewayWithRawEncryptedAsync(factory, projectId,
            apiKeyEncrypted: protector.Protect("dcg_test_api_key"),
            webhookSecretEncrypted: protector.Protect(webhookSecret));
    }

    private static async Task SeedGatewayWithRawEncryptedAsync(
        PlatformApiFactory factory, string projectId,
        string apiKeyEncrypted, string webhookSecretEncrypted)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
        {
            BranchPaymentGatewayId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            DcgateProjectId = projectId,
            ApiKeyEncrypted = apiKeyEncrypted,
            WebhookSecretEncrypted = webhookSecretEncrypted,
            CardLast4 = "1953",
            Status = AFK4.Platform.Api.Payments.BranchPaymentGatewayStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedDcGateIntentAsync(
        PlatformApiFactory factory, string state = "pending", long amountMinor = 5_000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Gateway Player",
            PhoneNumber = $"+99293000{playerId.ToString("N")[..4]}",
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
            AmountMinorUnits = amountMinor,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "dcgate",
            GatewayPaymentId = "pay_fake",
            GatewayComment = "AFK4-CMT-0001",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return intentId;
    }

    private static async Task SeedOpenShiftAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50_000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test",
            ClosingNote = string.Empty,
            OpenedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static string PaidBody(Guid intentId, string eventId) =>
        $$$"""{"eventId":"{{{eventId}}}","eventType":"payment.paid","projectId":"afk4","payment":{"id":"pay_fake","amount":"50.00","comment":"AFK4-CMT-0001","currency":"TJS","externalOrderId":"{{{intentId:N}}}","paidAt":"2026-06-03T12:00:00Z","status":"paid"}}""";

    private static string ExpiredBody(Guid intentId, string eventId) =>
        $$$"""{"eventId":"{{{eventId}}}","eventType":"payment.expired","projectId":"afk4","payment":{"id":"pay_fake","amount":"50.00","comment":"AFK4-CMT-0001","currency":"TJS","externalOrderId":"{{{intentId:N}}}","status":"expired"}}""";

    private static HttpRequestMessage SignedRequest(string body, string secret, string projectId = "afk4")
    {
        var sig = Convert.ToHexString(
            new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/public/payments/dcgate/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-dcgate-signature", $"sha256={sig}");
        req.Headers.Add("x-dcgate-project-id", projectId);
        return req;
    }

    private static async Task<int> CountTopUpsAsync(PlatformApiFactory factory, Guid intentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerId = (await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId)).PlayerAccountId;
        return await db.LedgerEntries.CountAsync(e => e.PlayerAccountId == playerId && e.EntryType == "top_up");
    }

    [Fact]
    public async Task Webhook_ValidPaid_CreditsOnceAndFulfilsIntent()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_paid_1");
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
        Assert.Equal("fulfilled", intent.State);
        Assert.NotNull(intent.FulfilledAtUtc);
    }

    [Fact]
    public async Task Webhook_ValidPaid_CreditsWithoutOpenShift()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        // No open shift seeded: an online payment confirms 24/7, independent of cashier shifts.
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_paid_noshift");
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
        Assert.Equal("fulfilled", intent.State);
        var playerId = intent.PlayerAccountId;
        var entry = await db.LedgerEntries.SingleAsync(e => e.PlayerAccountId == playerId && e.EntryType == "top_up");
        Assert.Null(entry.ShiftId);
    }

    [Fact]
    public async Task Webhook_ReplaySameEventId_DoesNotDoubleCredit()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_dup");
        var r1 = await client.SendAsync(SignedRequest(body, WebhookSecret));
        var r2 = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_BadSignature_Returns401AndDoesNotCredit()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_bad");
        var response = await client.SendAsync(SignedRequest(body, "wrong-secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_MissingSignature_Returns401()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_nosig");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/public/payments/dcgate/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_UnknownProjectId_Returns401AndDoesNotCredit()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_unknown_project");
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret, projectId: "unknown_project"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_UnknownExternalOrderId_Returns200Noop()
    {
        await using var factory = CreateFactory();
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);
        using var client = factory.CreateClient();

        var unknown = Guid.NewGuid();
        var body = PaidBody(unknown, "evt_unknown");
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Expired_MarksIntentExpired()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        using var client = factory.CreateClient();

        var body = ExpiredBody(intentId, "evt_exp");
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
        Assert.Equal("expired", intent.State);
    }

    [Fact]
    public async Task Webhook_UnknownEventType_Returns200Ack()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        using var client = factory.CreateClient();

        var body = $$$"""{"eventId":"evt_unknown_type","eventType":"payment.refunded","projectId":"afk4","payment":{"id":"pay_fake","amount":"50.00","comment":"AFK4-CMT-0001","currency":"TJS","externalOrderId":"{{{intentId:N}}}","status":"refunded"}}""";
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // unknown event type must not credit
        Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_Disputed_FlagsIntentAndLeavesStatePending()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        using var client = factory.CreateClient();

        var body = $$$"""{"eventId":"evt_disp","eventType":"payment.disputed","projectId":"afk4","payment":{"id":"pay_fake","amount":"50.00","comment":"AFK4-CMT-0001","currency":"TJS","externalOrderId":"{{{intentId:N}}}","status":"disputed"}}""";
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
        Assert.True(intent.Disputed);
        Assert.Equal("pending", intent.State);
    }

    // Cross-path double-confirm tests: prove that exactly one ledger top_up entry is
    // written regardless of which path (webhook vs. operator fulfil) fires first.

    [Fact]
    public async Task CrossPath_WebhookFirst_ThenOperatorFulfil_ExactlyOneCredit()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);

        // Step 1: webhook credits the wallet.
        using var webhookClient = factory.CreateClient();
        var body = PaidBody(intentId, "evt_cross_wf");
        var webhookResponse = await webhookClient.SendAsync(SignedRequest(body, WebhookSecret));
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
            Assert.Equal("fulfilled", intent.State);
        }

        // Step 2: operator fulfils the same (already-fulfilled) intent — must not double-credit.
        using var operatorClient = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, operatorClient, StaffRoleNames.CashierOperator);
        var fulfilResponse = await operatorClient.PostAsJsonAsync(
            $"/api/wallet/top-up-intents/{intentId}/fulfil", new { });

        // The fulfil endpoint returns 200 when the intent is already fulfilled (idempotency guard).
        Assert.Equal(HttpStatusCode.OK, fulfilResponse.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task CrossPath_OperatorFulfilFirst_ThenWebhook_ExactlyOneCredit()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        await SeedGatewayAsync(factory, "afk4", WebhookSecret);
        await SeedOpenShiftAsync(factory);

        // Step 1: operator fulfils the intent.
        using var operatorClient = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, operatorClient, StaffRoleNames.CashierOperator);
        var fulfilResponse = await operatorClient.PostAsJsonAsync(
            $"/api/wallet/top-up-intents/{intentId}/fulfil", new { });
        Assert.Equal(HttpStatusCode.OK, fulfilResponse.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
            Assert.Equal("fulfilled", intent.State);
        }

        // Step 2: webhook arrives for the same intent — must see State=="fulfilled" and no-op.
        using var webhookClient = factory.CreateClient();
        var body = PaidBody(intentId, "evt_cross_of");
        var webhookResponse = await webhookClient.SendAsync(SignedRequest(body, WebhookSecret));
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
    }

    [Fact]
    public async Task Webhook_CorruptWebhookSecret_Returns401AndDoesNotCredit()
    {
        await using var factory = CreateFactory();
        var intentId = await SeedDcGateIntentAsync(factory);
        // Seed a gateway whose WebhookSecretEncrypted is not a valid protected envelope.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var protector = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Security.ISecretProtector>();
            await SeedGatewayWithRawEncryptedAsync(factory, "afk4",
                apiKeyEncrypted: protector.Protect("dcg_test_api_key"),
                webhookSecretEncrypted: "not-a-valid-envelope");
        }
        using var client = factory.CreateClient();

        var body = PaidBody(intentId, "evt_corrupt_secret");
        var response = await client.SendAsync(SignedRequest(body, WebhookSecret));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
    }
}
