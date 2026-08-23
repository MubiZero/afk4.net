using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Банк отказал — игрок должен это понять, а не увидеть «что-то пошло не так».
///
/// Найдено живой сверкой с тестовым контуром Эсхаты 24.08.2026: при orderTypeId=3 кассу выдаёт
/// банк из своего пула, и пул бывает пуст — «Отсутствует свободная касса, повторите попытку
/// позже». Отказ приходит с HTTP 200 и `status:false`, клиент банка превращает его в исключение,
/// а исключение до сегодня доезжало до игрока пятисоткой.
///
/// Разница важна: «занято, попробуйте позже» — это подождать минуту или пойти к стойке, а
/// «сломалось» — это повторять одно и то же и злиться.
/// </summary>
public sealed class EskhataBankRefusalTests
{
    private const string Pin = "1234";

    [Fact]
    public async Task WhenTheBankRefusesTheOrder_ThePlayerIsToldToTryLater()
    {
        await using var factory = FactoryWith(new RefusingEskhataClient());
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.SeedMerchantConfigAsync(factory, player.OrgId);
        using var client = factory.CreateClient();
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(10_000, "TJS", "eskhata"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "online_payment_busy", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Незавершённая заявка не должна оставаться в кошельке: банк её не принял, платить по ней
    /// нечем, а в списке она выглядела бы как ожидающая оплата.
    /// </summary>
    [Fact]
    public async Task ARefusedOrder_LeavesNoPendingIntentBehind()
    {
        await using var factory = FactoryWith(new RefusingEskhataClient());
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.SeedMerchantConfigAsync(factory, player.OrgId);
        using var client = factory.CreateClient();
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(10_000, "TJS", "eskhata"));

        var intents = await client.GetFromJsonAsync<List<PlayerTopUpIntentDto>>(
            "/api/me/wallet/top-up-intents");
        Assert.Empty(intents!);
    }

    /// <summary>Стойка при этом работает: она не зависит ни от банка, ни от его касс.</summary>
    [Fact]
    public async Task TheCounterStillTakesMoneyWhenTheBankIsBusy()
    {
        await using var factory = FactoryWith(new RefusingEskhataClient());
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.SeedMerchantConfigAsync(factory, player.OrgId);
        using var client = factory.CreateClient();
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(10_000, "TJS", "counter"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static PlatformApiFactory FactoryWith(IEskhataMerchantClient gateway) =>
        new(extraServices: services =>
        {
            services.RemoveAll<IEskhataMerchantClientFactory>();
            services.AddSingleton<IEskhataMerchantClientFactory>(new StubFactory(gateway));
        });

    /// <summary>Банк, у которого нет свободной кассы, — дословно то, что отвечает тестовый контур.</summary>
    private sealed class RefusingEskhataClient : IEskhataMerchantClient
    {
        public Task<EskhataCreateOrderResult> CreateOrderAsync(string invoiceId, long amountMinor,
            string currencyCode, string description, int merchantId, CancellationToken ct) =>
            throw new HttpRequestException("Eskhata: Отсутствует свободная касса, повторите попытку позже");

        public Task<string?> GetOrderStatusAsync(string invoiceId, string orderId, long amountMinor,
            string currencyCode, int posId, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed class StubFactory(IEskhataMerchantClient client) : IEskhataMerchantClientFactory
    {
        public Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid organizationId, CancellationToken ct)
            => Task.FromResult<IEskhataMerchantClient?>(client);
    }
}
