using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Чем клуб принимает деньги — приложение спрашивает заранее, а не узнаёт отказом.
///
/// Онлайн-оплата держится на двух вещах сразу: тариф платформы её разрешает и у клуба заведён
/// мерчант банка. Ни то, ни другое приложению не видно, и до сих пор единственным способом
/// выяснить было нажать «оплатить онлайн» и получить 409. Предложить человеку кнопку, которая
/// откажет, — хуже, чем не показывать её вовсе.
/// </summary>
public sealed class PlayerTopUpMethodsTests
{
    private const string Pin = "1234";

    [Fact]
    public async Task WithMerchantConfigured_OnlinePaymentIsOffered()
    {
        await using var factory = FactoryWithGateway(new StubEskhataClient());
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.SeedMerchantConfigAsync(factory, player.OrgId);
        using var client = factory.CreateClient();
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        var response = await client.GetAsync("/api/me/wallet/top-up-methods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var methods = await response.Content.ReadFromJsonAsync<PlayerTopUpMethodsDto>();
        Assert.True(methods!.Online);
        // Стойка принимает деньги всегда: это наличные в кассе, им не нужен ни банк, ни тариф.
        Assert.True(methods.Counter);
    }

    [Fact]
    public async Task WithoutMerchantConfig_OnlinePaymentIsNotOffered()
    {
        await using var factory = FactoryWithGateway(new StubEskhataClient());
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        using var client = factory.CreateClient();
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        var response = await client.GetAsync("/api/me/wallet/top-up-methods");

        var methods = await response.Content.ReadFromJsonAsync<PlayerTopUpMethodsDto>();
        Assert.False(methods!.Online);
        Assert.True(methods.Counter);
    }

    /// <summary>
    /// Тариф платформы выключил онлайн-пополнение — значит его нет, сколько бы мерчантов клуб
    /// ни завёл. Иначе кнопка обещала бы то, что закрыто на другом этаже.
    /// </summary>
    [Fact]
    public async Task WhenThePlanDoesNotIncludeOnlineTopUp_ItIsNotOffered()
    {
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<IEskhataMerchantClientFactory>();
            services.AddSingleton<IEskhataMerchantClientFactory>(
                new StubEskhataClientFactory(new StubEskhataClient()));
            services.RemoveAll<AFK4.Platform.Api.Platform.Entitlements.IOrganizationEntitlements>();
            services.AddSingleton<AFK4.Platform.Api.Platform.Entitlements.IOrganizationEntitlements>(
                new NoFeaturesEntitlements());
        });
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.SeedMerchantConfigAsync(factory, player.OrgId);
        using var client = factory.CreateClient();
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        var response = await client.GetAsync("/api/me/wallet/top-up-methods");

        var methods = await response.Content.ReadFromJsonAsync<PlayerTopUpMethodsDto>();
        Assert.False(methods!.Online);
    }

    private static PlatformApiFactory FactoryWithGateway(IEskhataMerchantClient? gateway) =>
        new(extraServices: services =>
        {
            services.RemoveAll<IEskhataMerchantClientFactory>();
            services.AddSingleton<IEskhataMerchantClientFactory>(new StubEskhataClientFactory(gateway));
        });

    private sealed class StubEskhataClient : IEskhataMerchantClient
    {
        public Task<EskhataCreateOrderResult> CreateOrderAsync(string invoiceId, long amountMinor,
            string currencyCode, string description, int merchantId, CancellationToken ct) =>
            Task.FromResult(new EskhataCreateOrderResult(
                "order-1", "NEW", "QR", "https://bank.test/api/invoices/abc", 1));

        public Task<string?> GetOrderStatusAsync(string invoiceId, string orderId, long amountMinor,
            string currencyCode, int posId, CancellationToken ct) => Task.FromResult<string?>("NEW");
    }

    private sealed class StubEskhataClientFactory(IEskhataMerchantClient? client) : IEskhataMerchantClientFactory
    {
        public Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid organizationId, CancellationToken ct)
            => Task.FromResult(client);
    }

    private sealed class NoFeaturesEntitlements : AFK4.Platform.Api.Platform.Entitlements.IOrganizationEntitlements
    {
        public Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<AFK4.Shared.Contracts.Platform.Features.OrganizationFeatureStateDto>> DescribeAsync(
            Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
