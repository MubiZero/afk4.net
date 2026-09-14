using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Центр уведомлений. До него пуш был единственным способом узнать о событии, и пропущенный пуш —
/// выключенные уведомления, переустановленное приложение, мёртвый токен — прочитать было негде.
/// </summary>
public sealed class PlayerNotificationFeedTests
{
    private const string Pin = "1234";

    private static async Task SeedNotificationAsync(
        PlatformApiFactory factory,
        TopUpTestData.SeededPlayer player,
        string templateKey,
        string subject,
        string body,
        string status,
        DateTimeOffset createdUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.NotificationOutbox.Add(new NotificationOutboxEntity
        {
            NotificationOutboxId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Channel = "Push",
            Category = "Operational",
            TemplateKey = templateKey,
            Locale = "ru",
            RecipientAddress = string.Empty,
            PlayerAccountId = player.PlayerId,
            OrganizationId = player.OrgId,
            BranchId = player.BranchId,
            Subject = subject,
            BodyText = body,
            BodyHtml = string.Empty,
            Status = status,
            NextAttemptUtc = createdUtc,
            CreatedUtc = createdUtc
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Notifications_AreListedNewestFirstAndAllUnreadAtFirst()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedNotificationAsync(factory, player, "player.balance_topped_up", "Кошелёк пополнен",
            "На кошелёк зачислено 50 с.", NotificationOutboxStatus.Sent, DateTimeOffset.UtcNow.AddHours(-2));
        await SeedNotificationAsync(factory, player, "player.order_ready", "Заказ готов",
            "Кола ждёт вас на PC-01.", NotificationOutboxStatus.Sent, DateTimeOffset.UtcNow.AddHours(-1));

        var feed = await client.GetFromJsonAsync<PlayerNotificationsDto>("/api/me/notifications");

        Assert.Equal(2, feed!.Notifications.Count);
        Assert.Equal("Заказ готов", feed.Notifications[0].Subject);
        Assert.Equal(2, feed.UnreadCount);
    }

    // Та самая дыра: пуш не доехал — до сих пор об этом нельзя было узнать нигде.
    [Fact]
    public async Task UndeliveredNotification_IsStillReadable()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedNotificationAsync(factory, player, "player.session_ending", "Сессия заканчивается",
            "Осталось 10 минут.", NotificationOutboxStatus.Failed, DateTimeOffset.UtcNow);

        var feed = await client.GetFromJsonAsync<PlayerNotificationsDto>("/api/me/notifications");

        Assert.Equal("Сессия заканчивается", Assert.Single(feed!.Notifications).Subject);
    }

    // А подавленное — это то, от чего человек сам отказался: вернуть его в список значит вернуть
    // ему то, что он выключил.
    [Fact]
    public async Task SuppressedNotification_IsNotShown()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedNotificationAsync(factory, player, "player.reservation_soon", "Бронь скоро",
            "Через час.", NotificationOutboxStatus.Suppressed, DateTimeOffset.UtcNow);

        var feed = await client.GetFromJsonAsync<PlayerNotificationsDto>("/api/me/notifications");

        Assert.Empty(feed!.Notifications);
    }

    [Fact]
    public async Task OpeningTheFeed_MarksEverythingInItRead()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedNotificationAsync(factory, player, "player.order_ready", "Заказ готов",
            "Кола ждёт вас.", NotificationOutboxStatus.Sent, DateTimeOffset.UtcNow.AddMinutes(-5));

        var marked = await client.PostAsync("/api/me/notifications/read", null);
        var feed = await client.GetFromJsonAsync<PlayerNotificationsDto>("/api/me/notifications");

        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(0, feed!.UnreadCount);
        Assert.False(Assert.Single(feed.Notifications).IsUnread);
    }

    // Пришедшее после того, как список открыли, снова непрочитано — иначе отметка «прочитал всё»
    // однажды закрыла бы и будущее.
    [Fact]
    public async Task NotificationArrivingAfterReading_IsUnreadAgain()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedNotificationAsync(factory, player, "player.order_ready", "Старое",
            "Было.", NotificationOutboxStatus.Sent, DateTimeOffset.UtcNow.AddMinutes(-5));
        await client.PostAsync("/api/me/notifications/read", null);

        await SeedNotificationAsync(factory, player, "player.session_ending", "Новое",
            "Пришло позже.", NotificationOutboxStatus.Sent, DateTimeOffset.UtcNow.AddMinutes(5));
        var feed = await client.GetFromJsonAsync<PlayerNotificationsDto>("/api/me/notifications");

        Assert.Equal(1, feed!.UnreadCount);
        Assert.True(feed.Notifications[0].IsUnread);
        Assert.Equal("Новое", feed.Notifications[0].Subject);
    }

    [Fact]
    public async Task AnotherPlayersNotifications_AreNotShown()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        var stranger = player with { PlayerId = Guid.NewGuid() };
        await SeedNotificationAsync(factory, stranger, "player.order_ready", "Чужое",
            "Не ваше.", NotificationOutboxStatus.Sent, DateTimeOffset.UtcNow);

        var feed = await client.GetFromJsonAsync<PlayerNotificationsDto>("/api/me/notifications");

        Assert.Empty(feed!.Notifications);
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
