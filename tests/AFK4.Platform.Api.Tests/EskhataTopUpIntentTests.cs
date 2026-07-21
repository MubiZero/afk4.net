using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataTopUpIntentTests
{
    private sealed class FakeEskhataClient : IEskhataMerchantClient
    {
        public int Calls { get; private set; }
        public string? LastInvoiceId { get; private set; }
        public long LastAmountMinor { get; private set; }
        public int LastMerchantId { get; private set; }

        public Task<EskhataCreateOrderResult> CreateOrderAsync(string invoiceId, long amountMinor,
            string currencyCode, string description, int merchantId, CancellationToken ct)
        {
            Calls++;
            LastInvoiceId = invoiceId;
            LastAmountMinor = amountMinor;
            LastMerchantId = merchantId;
            return Task.FromResult(new EskhataCreateOrderResult(
                "order-abc", "NEW", "QRTEXT",
                "https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH", 48741));
        }

        public Task<string?> GetOrderStatusAsync(string invoiceId, string orderId, long amountMinor,
            string currencyCode, int posId, CancellationToken ct) => Task.FromResult<string?>("COMPLETED");
    }

    private sealed class FakeEskhataClientFactory(IEskhataMerchantClient? client) : IEskhataMerchantClientFactory
    {
        public Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid organizationId, CancellationToken ct)
            => Task.FromResult(client);
    }

    private static PlatformApiFactory CreateGatewayFactory(IEskhataMerchantClient? fake) =>
        new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<IEskhataMerchantClientFactory>();
            services.AddSingleton<IEskhataMerchantClientFactory>(new FakeEskhataClientFactory(fake));
        });

    private static async Task SeedMerchantConfigAsync(PlatformApiFactory factory, Guid orgId, int merchantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Security.ISecretProtector>();
        db.EskhataMerchantConfigs.Add(new EskhataMerchantConfigEntity
        {
            EskhataMerchantConfigId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = null,
            BaseUrl = "https://online3.eskhata.com:1444",
            CompanyId = "test-company",
            MerchantId = merchantId,
            HashKeyEncrypted = protector.Protect("test-hash-key"),
            Status = EskhataMerchantConfigStatus.Configured,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
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
        return new SeededPlayer(org, branch, player, phone);
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
    public async Task TopUpIntent_Eskhata_CreatesOrder_ReturnsQrAndDeepLink()
    {
        var fake = new FakeEskhataClient();
        await using var factory = CreateGatewayFactory(fake);
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedMerchantConfigAsync(factory, p.OrgId, merchantId: 28652);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS", "eskhata"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("eskhata", dto!.Method);
        Assert.Equal("QRTEXT", dto.Qr);
        Assert.Equal("eskhata://pay/hlR2oH", dto.DeepLink);

        Assert.Equal(1, fake.Calls);
        Assert.Equal(dto.PaymentIntentId.ToString("N"), fake.LastInvoiceId);
        Assert.Equal(5_000, fake.LastAmountMinor);
        Assert.Equal(28652, fake.LastMerchantId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.FindAsync(dto.PaymentIntentId);
        Assert.Equal("order-abc", intent!.GatewayPaymentId);
        Assert.Equal(48741, intent.GatewayPosId);
        Assert.Equal("QRTEXT", intent.GatewayQrPayload);
        Assert.Equal("https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH", intent.GatewayPayUrl);
    }

    [Fact]
    public async Task TopUpIntent_EskhataWithoutClient_ReturnsUnavailableAndDoesNotCallGateway()
    {
        var factory = CreateGatewayFactory(fake: null);
        var p = await SeedPlayerAsync(factory, "1234");
        using var httpClient = factory.CreateClient();
        await AuthenticateAsync(httpClient, p.OrgId, p.Phone, "1234");

        var response = await httpClient.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS", "eskhata"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PaymentIntents.AnyAsync());
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task TopUpIntent_EskhataWithoutMerchantConfig_ReturnsUnavailableAndDoesNotCallGateway()
    {
        var fake = new FakeEskhataClient();
        await using var factory = CreateGatewayFactory(fake);
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS", "eskhata"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, fake.Calls);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PaymentIntents.AnyAsync());
    }
}
