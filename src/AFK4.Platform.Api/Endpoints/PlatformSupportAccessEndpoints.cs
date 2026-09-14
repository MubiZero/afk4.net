using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Support;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformSupportAccessEndpoints
{
    public static void MapPlatformSupportAccessEndpoints(this WebApplication app)
    {
        app.MapPost("/api/platform/support-access-grants", async (
            CreatePlatformSupportAccessGrantRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformSupportAccessGrantService grantService,
            IAuditRecordWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UseSupportAccess);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var issue = await grantService.IssueAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, request, cancellationToken);
            if (issue is null) return Results.BadRequest();

            await WriteAuditAsync(auditWriter, issue.Grant.OrganizationId, authorization.PlatformAdminContext.PlatformAdminUserId,
                AuditActionNames.GrantPlatformSupportAccess, issue.Grant.GrantId, issue.Grant.Reason, cancellationToken);
            return Results.Ok(issue);
        }).RequirePlatformDomain();

        // Выданный доступ был невидим: список не существовал ни на сервере, ни в панели, и на
        // вопрос «кто сейчас внутри этого клуба» ответа не было ни у кого.
        app.MapGet("/api/platform/support-access-grants", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformSupportAccessGrantService grantService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UseSupportAccess);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await grantService.ListActiveAsync(organizationId, cancellationToken));
        }).RequirePlatformDomain();

        app.MapDelete("/api/platform/support-access-grants/{grantId:guid}", async (
            Guid grantId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformSupportAccessGrantService grantService,
            IAuditRecordWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UseSupportAccess);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            // Свой доступ обрывает кто угодно из поддержки, чужой — только распорядитель учётных
            // записей: он и так может отключить выдавшего целиком, так что ничего нового это ему
            // не даёт, зато доступ уехавшего домой коллеги перестаёт быть неприкосновенным.
            var allowAnyIssuer = authorization.PlatformAdminContext!.Permissions
                .Contains(PlatformAdminPermissionNames.ManagePlatformAdmins);
            var grant = await grantService.RevokeAsync(
                grantId, authorization.PlatformAdminContext.PlatformAdminUserId, allowAnyIssuer, cancellationToken);
            if (grant is null) return Results.NotFound();

            await WriteAuditAsync(auditWriter, grant.OrganizationId, authorization.PlatformAdminContext.PlatformAdminUserId,
                AuditActionNames.RevokePlatformSupportAccess, grant.GrantId, grant.Reason, cancellationToken);
            return Results.NoContent();
        }).RequirePlatformDomain();
    }

    private static Task WriteAuditAsync(
        IAuditRecordWriter writer,
        Guid organizationId,
        Guid actorId,
        string action,
        Guid grantId,
        string reason,
        CancellationToken cancellationToken) => writer.WriteAsync(new AuditRecordWriteRequest(
            organizationId, null, null, action, "PlatformSupportAccessGrant", grantId.ToString("D"),
            "Succeeded", "PlatformApi", JsonSerializer.Serialize(new { grantId, reason }))
        { ActorPlatformAdminUserId = actorId }, cancellationToken);
}
