using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static NotificationService CreateService(PlatformDbContext db) =>
        new(
            new EfNotificationOutbox(db),
            new EmbeddedTemplateProvider(defaultLocale: "ru"),
            new NotificationRenderer(),
            new FixedTimeProvider(Now),
            Options.Create(new NotificationOptions { DefaultLocale = "ru" }));

    private static NotificationRequest Request(
        string idempotencyKey = "test:1",
        string locale = "ru",
        string? email = "player@example.com",
        IReadOnlyList<NotificationChannel>? channels = null) => new(
        TemplateKey: NotificationTemplateKeys.Test,
        Category: NotificationCategory.Transactional,
        Recipient: new NotificationRecipient(Locale: locale, EmailAddress: email),
        Tokens: new Dictionary<string, string> { ["recipient"] = "Sam" },
        IdempotencyKey: idempotencyKey,
        PreferredChannels: channels);

    [Fact]
    public async Task SendAsync_EnqueuesOnePendingRowPerChannelWithRenderedSnapshot()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var handle = await service.SendAsync(Request(channels: [NotificationChannel.Email]), CancellationToken.None);

        Assert.True(handle.Created);
        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal("Email", row.Channel);
        Assert.Equal(NotificationOutboxStatus.Pending, row.Status);
        Assert.Equal("player@example.com", row.RecipientAddress);
        Assert.Equal("Проверка уведомлений AFK4", row.Subject);
        Assert.Contains("Sam", row.BodyText, StringComparison.Ordinal);
        Assert.Equal(Now, row.CreatedUtc);
        Assert.Equal(Now, row.NextAttemptUtc);
        Assert.Contains(row.NotificationOutboxId, handle.OutboxIds);
    }

    [Fact]
    public async Task SendAsync_DefaultsToEmailWhenNoPreferredChannels()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.SendAsync(Request(channels: null), CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal("Email", row.Channel);
    }

    [Fact]
    public async Task SendAsync_IsIdempotentOnIdempotencyKey()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var first = await service.SendAsync(Request(idempotencyKey: "dup"), CancellationToken.None);
        var second = await service.SendAsync(Request(idempotencyKey: "dup"), CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(1, await db.NotificationOutbox.CountAsync());
        Assert.Equal(first.OutboxIds, second.OutboxIds);
    }

    [Fact]
    public async Task SendAsync_ResolvesBlankLocaleToDefault()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.SendAsync(Request(locale: string.Empty), CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal("ru", row.Locale);
        Assert.Equal("Проверка уведомлений AFK4", row.Subject);
    }

    [Fact]
    public async Task SendAsync_RendersWithRequestedLocale()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.SendAsync(Request(locale: "en"), CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal("AFK4 notification check", row.Subject);
    }

    [Fact]
    public async Task SendAsync_SuppressesEmailRowWhenNoAddressOnFile()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.SendAsync(Request(email: null, channels: [NotificationChannel.Email]), CancellationToken.None);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Suppressed, row.Status);
        Assert.Contains("email", row.LastError, StringComparison.OrdinalIgnoreCase);
    }
}
