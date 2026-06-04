using System;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class DcGateTopUpIntentTests
{
    [Fact]
    public async Task PaymentIntent_PersistsGatewayColumns()
    {
        await using var factory = new PlatformApiFactory();
        var intentId = Guid.NewGuid();
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PaymentIntents.Add(new PaymentIntentEntity
            {
                PaymentIntentId = intentId,
                PlayerAccountId = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                BranchId = Guid.NewGuid(),
                AmountMinorUnits = 5_000,
                CurrencyCode = "TJS",
                Purpose = "wallet_topup",
                State = "pending",
                Method = "dcgate",
                GatewayPaymentId = "pay_abc123",
                GatewayPayUrl = "http://pay.dc.tj/?A=1&s=50.00&c=cmt",
                GatewayComment = "AFK4-COMMENT-0001",
                GatewayExpiresAtUtc = expires,
                Disputed = false,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var stored = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
            Assert.Equal("dcgate", stored.Method);
            Assert.Equal("pay_abc123", stored.GatewayPaymentId);
            Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", stored.GatewayPayUrl);
            Assert.Equal("AFK4-COMMENT-0001", stored.GatewayComment);
            Assert.NotNull(stored.GatewayExpiresAtUtc);
            Assert.False(stored.Disputed);
        }
    }

    [Fact]
    public async Task DcGateWebhookEvent_PersistsByEventId()
    {
        await using var factory = new PlatformApiFactory();
        var eventId = "evt_001";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.DcGateWebhookEvents.Add(new DcGateWebhookEventEntity
            {
                DcGateWebhookEventId = Guid.NewGuid(),
                EventId = eventId,
                EventType = "payment.paid",
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.True(await db.DcGateWebhookEvents.AnyAsync(e => e.EventId == eventId));
        }
    }
}
