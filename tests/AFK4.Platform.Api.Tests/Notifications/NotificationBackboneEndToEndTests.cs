using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>
/// End-to-end through the whole backbone wired the way Program.cs wires it: NotificationService ->
/// outbox -> dispatcher -> the real SmtpEmailChannel over a fake SMTP transport. Proves a trigger
/// produces a delivered, correctly-rendered email and that the path is idempotent.
/// </summary>
public sealed class NotificationBackboneEndToEndTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");

    private sealed class CapturingTransport : ISmtpTransport
    {
        public List<SmtpMessage> Sent { get; } = [];

        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Trigger_FlowsThroughOutboxAndDispatcherToTheEmailChannel()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var options = Options.Create(new NotificationOptions
        {
            DefaultLocale = "ru",
            FromAddress = "noreply@afk4.net",
            FromName = "AFK4",
        });
        var time = new FixedTimeProvider(Now);
        var outbox = new EfNotificationOutbox(db);
        var transport = new CapturingTransport();
        var channel = new SmtpEmailChannel(transport, options);
        var runner = new NotificationDispatchRunner(outbox, [channel], time, options);
        var service = new NotificationService(
            outbox, new EmbeddedTemplateProvider("ru"), new NotificationRenderer(),
            new EfNotificationPreferenceService(db, time), runner, time, options);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.Test,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(Locale: "en", EmailAddress: "owner@club.example"),
            Tokens: new Dictionary<string, string> { ["recipient"] = "Owner" },
            IdempotencyKey: "notification-test:club-1");

        var handle = await service.SendAsync(request, CancellationToken.None);
        Assert.True(handle.Created);

        var processed = await runner.RunAsync(max: 50, CancellationToken.None);

        Assert.Equal(1, processed);
        var message = Assert.Single(transport.Sent);
        Assert.Equal("owner@club.example", message.ToAddress);
        Assert.Equal("noreply@afk4.net", message.FromAddress);
        Assert.Equal("AFK4.NET notification check", message.Subject);
        Assert.Contains("Owner", message.BodyText, StringComparison.Ordinal);
        Assert.Contains("Owner", message.BodyHtml, StringComparison.Ordinal);

        var row = await db.NotificationOutbox.SingleAsync();
        Assert.Equal(NotificationOutboxStatus.Sent, row.Status);
        Assert.Equal(Now, row.SentUtc);
    }

    [Fact]
    public async Task DuplicateTrigger_IsIdempotentAcrossTheWholeChain()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var options = Options.Create(new NotificationOptions { DefaultLocale = "ru", FromAddress = "noreply@afk4.net" });
        var time = new FixedTimeProvider(Now);
        var outbox = new EfNotificationOutbox(db);
        var runner = new NotificationDispatchRunner(outbox, [new SmtpEmailChannel(new CapturingTransport(), options)], time, options);
        var service = new NotificationService(
            outbox, new EmbeddedTemplateProvider("ru"), new NotificationRenderer(),
            new EfNotificationPreferenceService(db, time), runner, time, options);

        NotificationRequest Request() => new(
            TemplateKey: NotificationTemplateKeys.Test,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(Locale: "ru", EmailAddress: "owner@club.example"),
            Tokens: new Dictionary<string, string> { ["recipient"] = "Owner" },
            IdempotencyKey: "notification-test:dup");

        await service.SendAsync(Request(), CancellationToken.None);
        await service.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(1, await db.NotificationOutbox.CountAsync());
    }
}
