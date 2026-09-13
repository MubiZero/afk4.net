using System.Net.Mail;
using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Health;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformHealthEndpoints
{
    public static void MapPlatformHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/health/overview", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformHealthOverviewService overviewService,
            CancellationToken cancellationToken) =>
        {
            // Право проверяется ДО обращения к данным — не после.
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewPlatformHealth);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await overviewService.GetOverviewAsync(cancellationToken));
        });

        // Проверка почты боевым путём: тот же шаблон, тот же канал, тот же SMTP. Отправляется
        // синхронно, чтобы причина отказа вернулась сразу, а не оседала в `LastError` строки очереди.
        app.MapPost("/api/platform/health/test-email", async (
            SendTestEmailRequest request,
            PlatformAdminAuthorizationService authorizationService,
            INotificationService notifications,
            IAuditRecordWriter auditRecordWriter,
            IOptions<NotificationOptions> notificationOptions,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.SendTestNotification);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var email = request.Email?.Trim() ?? string.Empty;
            if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            {
                return Results.BadRequest(new { error = "A valid email address is required." });
            }

            var delivery = await notifications.SendNowAsync(
                new NotificationRequest(
                    TemplateKey: NotificationTemplateKeys.Test,
                    Category: NotificationCategory.Transactional,
                    Recipient: new NotificationRecipient(
                        Locale: notificationOptions.Value.DefaultLocale,
                        EmailAddress: email),
                    Tokens: new Dictionary<string, string>(),
                    // Свой ключ на каждую проверку: повторная проверка обязана слать письмо заново,
                    // иначе после починки настроек она вернёт старый результат.
                    IdempotencyKey: $"notification-test:{Guid.NewGuid():N}",
                    PreferredChannels: [NotificationChannel.Email]),
                cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: Guid.Empty,
                BranchId: null,
                ActorStaffUserId: null,
                Action: AuditActionNames.PlatformSendTestEmail,
                TargetType: "NotificationChannel",
                TargetId: email,
                Outcome: delivery.Delivered ? AuditOutcome.Succeeded : AuditOutcome.Failed,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new { email, delivery.Error })),
                cancellationToken);

            return Results.Ok(new SendTestEmailResultDto(delivery.Delivered, delivery.Error));
        }).RequireRateLimiting("platform-test-email");
    }
}
