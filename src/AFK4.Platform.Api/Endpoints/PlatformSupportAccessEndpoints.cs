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

            var grant = await grantService.CreateAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, request, cancellationToken);
            if (grant is null) return Results.BadRequest();

            await WriteAuditAsync(auditWriter, grant.OrganizationId, authorization.PlatformAdminContext.PlatformAdminUserId,
                AuditActionNames.GrantPlatformSupportAccess, grant.GrantId, grant.Reason, cancellationToken);
            return Results.Ok(grant);
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

            var grant = await grantService.RevokeAsync(
                grantId, authorization.PlatformAdminContext!.PlatformAdminUserId, cancellationToken);
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
