using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.AspNetCore.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class SupportAccessSessionEndpoints
{
    public static void MapSupportAccessSessionEndpoints(this WebApplication app)
    {
        // Публичный: у админки клиента на этом шаге ещё нет ничего, кроме билета.
        app.MapPost("/api/public/support-access/sessions", async (
            RedeemSupportAccessTicketRequest request,
            PlatformSupportAccessGrantService supportAccessService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var session = await supportAccessService.RedeemTicketAsync(request.Ticket, cancellationToken);
            if (session is null)
            {
                // Билет не опознан: организации у нас нет, но попытка входа по чужому или
                // просроченному билету — ровно то, что ищут при разборе.
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: null,
                    action: AuditActionNames.RedeemPlatformSupportAccess,
                    targetType: "PlatformSupportAccessGrant",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { Reason = "ticket_not_valid" },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Выдача доступа и вход по нему — разные события. До сих пор в журнале было видно
            // только первое: клуб не мог узнать, воспользовались доступом или нет.
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: session.OrganizationId,
                actorPlatformAdminUserId: null,
                action: AuditActionNames.RedeemPlatformSupportAccess,
                targetType: "PlatformSupportAccessGrant",
                targetId: session.OrganizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { session.Reason, session.ExpiresAtUtc },
                cancellationToken);

            return Results.Ok(session);
        }).RequireRateLimiting("player-public");

        app.MapDelete("/api/support-access/session", async (
            IPlatformSupportContextAccessor supportContextAccessor,
            PlatformSupportAccessGrantService supportAccessService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var support = supportContextAccessor.Current;
            if (support is null)
            {
                return Results.Unauthorized();
            }

            // Выход из своей же сессии: расширять область до чужих доступов здесь незачем и нечем —
            // сессия знает ровно один грант, свой.
            await supportAccessService.RevokeAsync(
                support.GrantId, support.PlatformAdminUserId, allowAnyIssuer: false, cancellationToken);

            // Закрытие доступа через панель аудировалось, а самостоятельный выход — нет, и в
            // журнале оставалось «вошёл» без «вышел».
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: support.OrganizationId,
                actorPlatformAdminUserId: support.PlatformAdminUserId,
                action: AuditActionNames.EndPlatformSupportSession,
                targetType: "PlatformSupportAccessGrant",
                targetId: support.GrantId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { Via = "self" },
                cancellationToken);

            return Results.NoContent();
        }).AllowPlatformSupportAccess(PlatformSupportSelfPermission);
    }

    // Собственные эндпоинты сессии не требуют прав клуба: это управление самой сессией.
    private const string PlatformSupportSelfPermission = "organization.support_access.self";
}
