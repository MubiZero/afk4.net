using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAlertNotifierTests
{
    private sealed class CapturingSmtp : ISmtpTransport
    {
        public List<SmtpMessage> Sent { get; } = [];
        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingSms : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static PlatformIncidentEntity Incident(string kind, string severity) => new()
    {
        PlatformIncidentId = Guid.NewGuid(),
        Kind = kind,
        DedupKey = kind + ":x",
        Severity = severity,
        DetailsJson = "{\"minutes\":180}",
        OpenedAtUtc = DateTimeOffset.UtcNow,
        LastSeenAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task CriticalIncident_SendsSmsToConfiguredRecipients()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch@platform.test", "Watcher");

        var smtp = new CapturingSmtp();
        var sms = new CapturingSms();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            smtp, sms,
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions { SmsRecipients = ["+992900000000"] }),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.NotificationQueueStuck, PlatformIncidentSeverityNames.Critical),
            CancellationToken.None);

        Assert.NotEmpty(smtp.Sent);
        Assert.Equal("+992900000000", Assert.Single(sms.Sent).ToPhoneNumber);
    }

    [Fact]
    public async Task WarningIncident_SendsEmailButNoSms()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch2@platform.test", "Watcher");

        var smtp = new CapturingSmtp();
        var sms = new CapturingSms();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            smtp, sms,
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions { SmsRecipients = ["+992900000000"] }),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.JobFailing, PlatformIncidentSeverityNames.Warning),
            CancellationToken.None);

        Assert.NotEmpty(smtp.Sent);
        Assert.Empty(sms.Sent);
    }

    [Fact]
    public async Task InactiveAdmin_DoesNotReceiveAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "active@platform.test", "Active");
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "fired@platform.test", "Fired", isActive: false);

        var smtp = new CapturingSmtp();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            smtp, new CapturingSms(),
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions()),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.JobFailing, PlatformIncidentSeverityNames.Warning),
            CancellationToken.None);

        Assert.DoesNotContain(smtp.Sent, message => message.ToAddress == "fired@platform.test");
    }
}
