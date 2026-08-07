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

    private sealed class ThrowingSmtp : ISmtpTransport
    {
        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken) =>
            throw new SmtpTransportException(isPermanent: false, "smtp unavailable");
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

    [Fact]
    public async Task SmtpFailure_DoesNotThrow_AndRecordsFailedJobRun()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch3@platform.test", "Watcher");

        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            new ThrowingSmtp(), new CapturingSms(),
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions { SmsRecipients = ["+992900000000"] }),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        var incident = Incident(PlatformIncidentKindNames.JobFailing, PlatformIncidentSeverityNames.Warning);
        await notifier.NotifyOpenedAsync(incident, CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var run = Assert.Single(dbContext.PlatformJobRuns.Where(r => r.JobName == PlatformJobNames.AlertDelivery));
        Assert.Equal(PlatformJobOutcomeNames.Failed, run.Outcome);
        Assert.False(string.IsNullOrEmpty(run.Error));
    }

    [Fact]
    public async Task SmtpFailure_CriticalSmsWorthyIncident_StillSendsSms()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch4@platform.test", "Watcher");

        var sms = new CapturingSms();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            new ThrowingSmtp(), sms,
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions { SmsRecipients = ["+992900000000"] }),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.NotificationQueueStuck, PlatformIncidentSeverityNames.Critical),
            CancellationToken.None);

        Assert.Equal("+992900000000", Assert.Single(sms.Sent).ToPhoneNumber);
    }

    [Fact]
    public async Task SuccessfulDelivery_RecordsSucceededJobRunWithDeliveredCount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch5@platform.test", "Watcher");

        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            new CapturingSmtp(), new CapturingSms(),
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions()),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.JobFailing, PlatformIncidentSeverityNames.Warning),
            CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var run = Assert.Single(dbContext.PlatformJobRuns.Where(r => r.JobName == PlatformJobNames.AlertDelivery));
        Assert.Equal(PlatformJobOutcomeNames.Succeeded, run.Outcome);
        Assert.Equal(1, run.ItemsProcessed);
        Assert.Null(run.Error);
    }

    [Fact]
    public async Task NotifyResolved_CriticalSmsWorthyIncident_SendsEmailButNoSms()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch6@platform.test", "Watcher");

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

        var incident = Incident(PlatformIncidentKindNames.NotificationQueueStuck, PlatformIncidentSeverityNames.Critical);
        incident.ResolvedAtUtc = DateTimeOffset.UtcNow;

        await notifier.NotifyResolvedAsync(incident, CancellationToken.None);

        Assert.NotEmpty(smtp.Sent);
        Assert.Empty(sms.Sent);
    }
}
