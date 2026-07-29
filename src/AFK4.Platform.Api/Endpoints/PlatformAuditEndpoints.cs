using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformAuditEndpoints
{
    public static void MapPlatformAuditEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/audit", async (
            Guid? organizationId, string? action, string? outcome, string? targetType,
            DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit,
            PlatformAdminAuthorizationService authorizationService,
            IAuditSearchService auditSearchService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewPlatformAudit);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty,
                    authorization.PlatformAdminContext!.PlatformAdminUserId, AuditActionNames.ViewAudit,
                    "AuditRecord", null, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit);
            var result = await auditSearchService.SearchPlatformAsync(organizationId, query, cancellationToken);
            await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty,
                authorization.PlatformAdminContext!.PlatformAdminUserId, AuditActionNames.ViewAudit,
                "AuditRecord", null, AuditOutcome.Succeeded,
                new { Scope = "platform", Count = result.Records.Count, result.Limit, organizationId, action, outcome, fromUtc, toUtc }, cancellationToken);
            return Results.Ok(result);
        });
    }
}
