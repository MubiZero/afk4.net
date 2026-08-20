using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Announcements;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Announcements;
using AFK4.Shared.Contracts.Platform.Auth;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Ведение анонсов платформы. Публикация и снятие видны снаружи — первая рассылает письма
/// владельцам, второе гасит полосу у всех операторов, — поэтому каждое действие и каждый отказ
/// ложатся в аудит.
/// </summary>
public static class PlatformAnnouncementEndpoints
{
    public static void MapPlatformAnnouncementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/announcements", async (
            PlatformAdminAuthorizationService authorizationService,
            PlatformAnnouncementService announcementService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAnnouncements);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteDeniedAsync(auditRecordWriter, authorization, AuditActionNames.ViewPlatformAnnouncements,
                    targetId: null, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await announcementService.ListAsync(cancellationToken));
        });

        app.MapPost("/api/platform/announcements", async (
            SavePlatformAnnouncementRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAnnouncementService announcementService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAnnouncements);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteDeniedAsync(auditRecordWriter, authorization, AuditActionNames.CreatePlatformAnnouncement,
                    targetId: null, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await announcementService.CreateAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, request, cancellationToken);

            await WriteOutcomeAsync(auditRecordWriter, authorization, AuditActionNames.CreatePlatformAnnouncement,
                result.Value?.AnnouncementId.ToString(), result, new { request.Severity, request.AudienceKind }, cancellationToken);

            return MapResult(result);
        });

        app.MapPut("/api/platform/announcements/{announcementId:guid}", async (
            Guid announcementId,
            SavePlatformAnnouncementRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAnnouncementService announcementService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAnnouncements);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteDeniedAsync(auditRecordWriter, authorization, AuditActionNames.UpdatePlatformAnnouncement,
                    announcementId.ToString(), cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await announcementService.UpdateAsync(announcementId, request, cancellationToken);

            await WriteOutcomeAsync(auditRecordWriter, authorization, AuditActionNames.UpdatePlatformAnnouncement,
                announcementId.ToString(), result, new { request.Severity, request.AudienceKind }, cancellationToken);

            return MapResult(result);
        });

        app.MapPost("/api/platform/announcements/{announcementId:guid}/publish", async (
            Guid announcementId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAnnouncementService announcementService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAnnouncements);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteDeniedAsync(auditRecordWriter, authorization, AuditActionNames.PublishPlatformAnnouncement,
                    announcementId.ToString(), cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await announcementService.PublishAsync(announcementId, cancellationToken);

            await WriteOutcomeAsync(auditRecordWriter, authorization, AuditActionNames.PublishPlatformAnnouncement,
                announcementId.ToString(), result,
                new { EmailDispatched = result.Value?.EmailDispatched }, cancellationToken);

            return MapResult(result);
        });

        app.MapPost("/api/platform/announcements/{announcementId:guid}/withdraw", async (
            Guid announcementId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAnnouncementService announcementService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageAnnouncements);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteDeniedAsync(auditRecordWriter, authorization, AuditActionNames.WithdrawPlatformAnnouncement,
                    announcementId.ToString(), cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await announcementService.WithdrawAsync(announcementId, cancellationToken);

            await WriteOutcomeAsync(auditRecordWriter, authorization, AuditActionNames.WithdrawPlatformAnnouncement,
                announcementId.ToString(), result, new { }, cancellationToken);

            return MapResult(result);
        });
    }

    // Код отказа едет и в Error, и в Code: транспорт панели читает машинный код из Error
    // (см. PlatformTransport.toError), Code оставлен для читаемости тела.
    private static IResult MapResult(AnnouncementResult result) =>
        result.ErrorCode switch
        {
            null => Results.Ok(result.Value),
            AnnouncementErrorCodes.InvalidAnnouncement
                or AnnouncementErrorCodes.UnknownPlan
                or AnnouncementErrorCodes.UnknownOrganization => Results.BadRequest(Body(result.ErrorCode)),
            _ => Results.Conflict(Body(result.ErrorCode))
        };

    private static object Body(string code) => new { Error = code, Code = code };

    private static Task WriteDeniedAsync(
        IAuditRecordWriter auditRecordWriter,
        PlatformAdminAuthorizationResult authorization,
        string action,
        string? targetId,
        CancellationToken cancellationToken) =>
        WriteAnnouncementAuditAsync(auditRecordWriter, authorization, action, targetId, AuditOutcome.Denied,
            new { authorization.DenialReason }, cancellationToken);

    /// <summary>
    /// Успех и отказ по существу пишутся одинаково: отказ «нельзя менять аудиторию у
    /// опубликованного» — это тоже попытка изменить то, что уже разослано, и она обязана остаться
    /// в следе, а не только в красной плашке.
    /// </summary>
    private static Task WriteOutcomeAsync(
        IAuditRecordWriter auditRecordWriter,
        PlatformAdminAuthorizationResult authorization,
        string action,
        string? targetId,
        AnnouncementResult result,
        object details,
        CancellationToken cancellationToken) =>
        result.Succeeded
            ? WriteAnnouncementAuditAsync(auditRecordWriter, authorization, action, targetId,
                AuditOutcome.Succeeded, details, cancellationToken)
            : WriteAnnouncementAuditAsync(auditRecordWriter, authorization, action, targetId,
                AuditOutcome.Denied, new { Reason = result.ErrorCode }, cancellationToken);

    private static Task WriteAnnouncementAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        PlatformAdminAuthorizationResult authorization,
        string action,
        string? targetId,
        string outcome,
        object details,
        CancellationToken cancellationToken) =>
        WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext?.PlatformAdminUserId,
            action: action,
            targetType: "PlatformAnnouncement",
            targetId: targetId,
            outcome: outcome,
            details: details,
            cancellationToken);
}
