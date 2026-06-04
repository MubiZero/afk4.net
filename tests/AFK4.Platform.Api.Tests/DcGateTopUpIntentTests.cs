using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.DcGate;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    private sealed class FakeDcGateClient : IDcGateClient
    {
        public int Calls { get; private set; }
        public string? LastExternalOrderId { get; private set; }
        public long LastAmountMinor { get; private set; }

        public Task<DcGatePaymentResult> CreatePaymentAsync(
            long amountMinorUnits, string currencyCode, string externalOrderId,
            object metadata, CancellationToken cancellationToken)
        {
            Calls++;
            LastExternalOrderId = externalOrderId;
            LastAmountMinor = amountMinorUnits;
            return Task.FromResult(new DcGatePaymentResult(
                PaymentId: "pay_fake",
                Status: "pending",
                Amount: "50.00",
                Currency: "TJS",
                Comment: "AFK4-CMT-0001",
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15),
                PayUrl: "http://pay.dc.tj/?A=1&s=50.00&c=cmt"));
        }
    }

    private static PlatformApiFactory CreateGatewayFactory(FakeDcGateClient fake) =>
        new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<IDcGateClient>();
            services.AddSingleton<IDcGateClient>(fake);
        });

    private sealed record SeededGatewayPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    private static async Task<SeededGatewayPlayer> SeedPlayerAsync(
        PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = $"+99290000{player.ToString("N")[..4]}";
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Test Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        credential.PasswordHash =
            new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
        return new SeededGatewayPlayer(org, branch, player, phone);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task TopUpIntent_WithDcGateMethod_CreatesGatewayPaymentAndReturnsPayUrl()
    {
        var fake = new FakeDcGateClient();
        await using var factory = CreateGatewayFactory(fake);
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS", "dcgate"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("dcgate", dto!.Method);
        Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", dto.PayUrl);
        Assert.Equal("AFK4-CMT-0001", dto.Comment);
        Assert.NotNull(dto.GatewayExpiresAtUtc);

        Assert.Equal(1, fake.Calls);
        Assert.Equal(dto.PaymentIntentId.ToString("N"), fake.LastExternalOrderId);
        Assert.Equal(5_000, fake.LastAmountMinor);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.FindAsync(dto.PaymentIntentId);
        Assert.Equal("pay_fake", intent!.GatewayPaymentId);
        Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", intent.GatewayPayUrl);
    }

    [Fact]
    public async Task TopUpIntent_DefaultMethod_StaysCounterAndDoesNotCallGateway()
    {
        var fake = new FakeDcGateClient();
        await using var factory = CreateGatewayFactory(fake);
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("counter", dto!.Method);
        Assert.Null(dto.PayUrl);
        Assert.Equal(0, fake.Calls);
    }
}
