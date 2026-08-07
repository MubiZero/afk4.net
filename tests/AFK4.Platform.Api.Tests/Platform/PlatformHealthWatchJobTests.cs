using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// End-to-end через реальный DI-граф (как его собирает Program.cs), кроме подменённого SMTP-
/// транспорта. Гоняем сторож напрямую через RunOnceAsync — как и другие периодические задания,
/// он не требует запущенного хоста.
/// </summary>
public sealed class PlatformHealthWatchJobTests
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

    private static PlatformHealthWatchJob BuildJob(PlatformApiFactory factory, TimeProvider time) => new(
        factory.Services,
        time,
        factory.Services.GetRequiredService<IOptions<PlatformHealthOptions>>(),
        factory.Services.GetRequiredService<IOptions<BillingOptions>>(),
        factory.Services.GetRequiredService<IOptions<NotificationOptions>>(),
        factory.Services.GetRequiredService<IOptions<OutboxOptions>>(),
        factory.Services.GetRequiredService<AutoProtectionOptions>(),
        NullLogger<PlatformHealthWatchJob>.Instance);

    private static async Task SeedFailedNotificationAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.NotificationOutbox.Add(new NotificationOutboxEntity
        {
            NotificationOutboxId = Guid.NewGuid(),
            IdempotencyKey = "health-watch-test:" + Guid.NewGuid(),
            Channel = "email",
            Category = "transactional",
            TemplateKey = "test",
            Locale = "ru",
            RecipientAddress = "owner@club.example",
            Subject = "test",
            BodyText = "test",
            BodyHtml = "test",
            Status = NotificationOutboxStatus.Failed,
            NextAttemptUtc = DateTimeOffset.UtcNow,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StuckNotificationQueue_OpensIncidentAndSendsAlert()
    {
        var smtp = new CapturingSmtp();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmtpTransport>();
            services.AddSingleton<ISmtpTransport>(smtp);
        });
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "health-watch@platform.test", "Watcher");
        await SeedFailedNotificationAsync(factory.Services);

        var job = BuildJob(factory, TimeProvider.System);
        await job.RunOnceAsync(CancellationToken.None);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var incident = Assert.Single(db.PlatformIncidents
            .Where(row => row.Kind == PlatformIncidentKindNames.NotificationQueueStuck));
        Assert.Null(incident.ResolvedAtUtc);

        Assert.NotEmpty(smtp.Sent);
    }

    [Fact]
    public async Task ProblemDisappearing_ClosesTheIncident()
    {
        var smtp = new CapturingSmtp();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmtpTransport>();
            services.AddSingleton<ISmtpTransport>(smtp);
        });
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "health-watch2@platform.test", "Watcher");
        await SeedFailedNotificationAsync(factory.Services);

        var job = BuildJob(factory, TimeProvider.System);
        await job.RunOnceAsync(CancellationToken.None);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            // InMemory provider doesn't support ExecuteDelete; RemoveRange+SaveChanges works everywhere.
            db.NotificationOutbox.RemoveRange(db.NotificationOutbox);
            await db.SaveChangesAsync();
        }

        await job.RunOnceAsync(CancellationToken.None);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var incident = Assert.Single(verifyDb.PlatformIncidents
            .Where(row => row.Kind == PlatformIncidentKindNames.NotificationQueueStuck));
        Assert.NotNull(incident.ResolvedAtUtc);

        Assert.DoesNotContain(
            await verifyDb.PlatformIncidents.Where(row => row.ResolvedAtUtc == null).ToListAsync(),
            row => row.Kind == PlatformIncidentKindNames.NotificationQueueStuck);
    }

    [Fact]
    public async Task JobIntervals_CoverEveryWatchedJob()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var job = BuildJob(factory, TimeProvider.System);

        Assert.Equal(
            PlatformJobNames.Watched.ToHashSet(StringComparer.Ordinal),
            job.JobIntervals.Keys.ToHashSet(StringComparer.Ordinal));
    }
}
