using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Отказаться от собственной заявки на пополнение игрок не мог никак: маршрута не было вовсе.
/// Заявка висела в карточке кошелька как ожидающая ровно сутки, пока её не признавали
/// просроченной по времени создания — и всё это время отвечала на вопрос «я же пополнял» «да».
/// </summary>
public sealed class PlayerTopUpCancelTests
{
    private const string Pin = "1234";

    private static async Task<Guid> SeedPendingIntentAsync(PlatformApiFactory factory, TopUpTestData.SeededPlayer player, string state = "pending")
    {
        var intentId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            OrganizationId = player.OrgId,
            BranchId = player.BranchId,
            PlayerAccountId = player.PlayerId,
            AmountMinorUnits = 10_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "dc",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return intentId;
    }

    private static async Task<string?> ReadStateAsync(PlatformApiFactory factory, Guid intentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PaymentIntents.Where(x => x.PaymentIntentId == intentId)
            .Select(x => x.State).SingleOrDefaultAsync();
    }

    [Fact]
    public async Task PendingIntent_CanBeCancelledByItsOwner()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        var intentId = await SeedPendingIntentAsync(factory, player);

        var response = await client.DeleteAsync($"/api/me/wallet/top-up-intents/{intentId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("cancelled", (await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>())!.State);
        Assert.Equal("cancelled", await ReadStateAsync(factory, intentId));
        var intents = await client.GetFromJsonAsync<PlayerTopUpIntentDto[]>("/api/me/wallet/top-up-intents");
        Assert.Equal("cancelled", Assert.Single(intents!).State);
    }

    // Повторное нажатие и вторая вкладка — обычная жизнь, а не ошибка.
    [Fact]
    public async Task CancellingTwice_IsNotAnError()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        var intentId = await SeedPendingIntentAsync(factory, player);
        await client.DeleteAsync($"/api/me/wallet/top-up-intents/{intentId:D}");

        var second = await client.DeleteAsync($"/api/me/wallet/top-up-intents/{intentId:D}");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // Деньги уже зачислены: отменять нечего, и делать вид, что можно, нельзя.
    [Fact]
    public async Task FulfilledIntent_CannotBeCancelled()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        var intentId = await SeedPendingIntentAsync(factory, player, state: "fulfilled");

        var response = await client.DeleteAsync($"/api/me/wallet/top-up-intents/{intentId:D}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("fulfilled", await ReadStateAsync(factory, intentId));
    }

    // Чужая заявка не существует для этого игрока — ни отменить, ни узнать, что она есть.
    [Fact]
    public async Task AnotherPlayersIntent_IsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        var stranger = player with { PlayerId = Guid.NewGuid() };
        var intentId = await SeedPendingIntentAsync(factory, stranger);

        var response = await client.DeleteAsync($"/api/me/wallet/top-up-intents/{intentId:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("pending", await ReadStateAsync(factory, intentId));
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/me/wallet/top-up-intents/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
