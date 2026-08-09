using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Announcements;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Platform.Announcements;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class AnnouncementConcurrencyPostgresTests
{
    // Двое администраторов жмут «опубликовать» на один анонс. Сторож рассылки — поле на строке
    // анонса, то есть чтение перед записью: без изоляции обе стороны увидят пустое
    // EmailDispatchedAtUtc и владелец получит ДВА одинаковых письма. Именно это и проверяется, и
    // проверить это можно только на настоящем Postgres: InMemory не знает ни транзакций, ни
    // изоляции, и «гонки» там не бывает вовсе.
    [PlatformAdminPostgresFact]
    public async Task ConcurrentPublish_SendsTheOwnerExactlyOneEmail()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"announcement_race_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString).ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scopedBuilder.ConnectionString)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            var now = DateTimeOffset.Parse("2026-08-10T08:00:00Z");
            var announcementId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();
            await using (var seedDb = new PlatformDbContext(options))
            {
                seedDb.Organizations.Add(new OrganizationEntity
                {
                    OrganizationId = organizationId,
                    Slug = "race-club",
                    Name = "Клуб",
                    Status = OrganizationStatusNames.Active,
                    PlanCode = "starter",
                    SubscriptionStatus = SubscriptionStatusNames.Trial,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                seedDb.PlatformAnnouncements.Add(new PlatformAnnouncementEntity
                {
                    PlatformAnnouncementId = announcementId,
                    Title = "Плановые работы",
                    Body = "Текст",
                    Severity = AnnouncementSeverity.Critical,
                    ShowFromUtc = now,
                    ShowUntilUtc = now.AddDays(3),
                    AudienceKind = AnnouncementAudienceKinds.All,
                    AudiencePlanCodesJson = "[]",
                    AudienceOrganizationIdsJson = "[]",
                    Status = AnnouncementStatus.Draft,
                    CreatedByPlatformAdminUserId = Guid.NewGuid(),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                await seedDb.SaveChangesAsync();
            }

            await using var dbLeft = new PlatformDbContext(options);
            await using var dbRight = new PlatformDbContext(options);
            var recipient = new OwnerRecipient(Guid.NewGuid(), "Владелец", "owner@club.test", "Клуб");
            var serviceLeft = new PlatformAnnouncementService(
                dbLeft, new FixedTimeProvider(now), new FixedOwnerResolver(recipient), new OutboxWritingNotificationService(dbLeft, now));
            var serviceRight = new PlatformAnnouncementService(
                dbRight, new FixedTimeProvider(now), new FixedOwnerResolver(recipient), new OutboxWritingNotificationService(dbRight, now));

            var results = await Task.WhenAll(
                serviceLeft.PublishAsync(announcementId, CancellationToken.None),
                serviceRight.PublishAsync(announcementId, CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Single(results, result => result.Succeeded);
            var loser = Assert.Single(results, result => !result.Succeeded);
            // Проигравший объясняется честно: либо анонс уже не черновик, либо serializable отменил
            // его транзакцию целиком. Сырого 500 быть не должно.
            Assert.Contains(loser.ErrorCode, new[] { AnnouncementErrorCodes.InvalidTransition, AnnouncementErrorCodes.Conflict });

            // Считаются строки в очереди, а не вызовы сервиса: письмо существует ровно тогда, когда
            // его строка пережила транзакцию. Две строки означали бы два одинаковых письма владельцу.
            await using var verifyDb = new PlatformDbContext(options);
            Assert.Equal(1, await verifyDb.NotificationOutbox
                .CountAsync(row => row.TemplateKey == NotificationTemplateKeys.PlatformAnnouncement));
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedOwnerResolver(OwnerRecipient recipient) : IOrganizationOwnerResolver
    {
        public Task<OwnerRecipient?> ResolveAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<OwnerRecipient?>(recipient);
    }

    /// <summary>
    /// Пишет строку в очередь через ТОТ ЖЕ контекст, что и настоящий <c>EfNotificationOutbox</c> —
    /// иначе тест мерил бы вызовы сервиса, а не письма: вставка проигравшей стороны уходит вместе
    /// с её откаченной транзакцией, и снаружи её не существует.
    /// </summary>
    private sealed class OutboxWritingNotificationService(PlatformDbContext dbContext, DateTimeOffset now) : INotificationService
    {
        public async Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken)
        {
            var row = new NotificationOutboxEntity
            {
                NotificationOutboxId = Guid.NewGuid(),
                IdempotencyKey = request.IdempotencyKey,
                Channel = NotificationChannel.Email.ToString(),
                Category = request.Category.ToString(),
                TemplateKey = request.TemplateKey,
                Locale = "ru",
                RecipientAddress = request.Recipient.EmailAddress ?? string.Empty,
                OrganizationId = request.OrganizationId,
                Subject = "subject",
                BodyText = "body",
                BodyHtml = "<p>body</p>",
                Status = NotificationOutboxStatus.Pending,
                NextAttemptUtc = now,
                CreatedUtc = now
            };

            dbContext.NotificationOutbox.Add(row);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new NotificationHandle([row.NotificationOutboxId], true);
        }

        public Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
