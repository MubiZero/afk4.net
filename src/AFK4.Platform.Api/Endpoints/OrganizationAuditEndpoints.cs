using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class OrganizationAuditEndpoints
{
    public static void MapOrganizationAuditEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("branches/{branchId:guid}/audit", async (
            Guid branchId,
            string? action,
            string? outcome,
            string? targetType,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            Guid? actorStaffUserId,
            long? minAmount,
            long? maxAmount,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IAuditSearchService auditSearchService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewAudit, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId,
                    authorization.StaffContext.StaffUserId, AuditActionNames.ViewAudit, "AuditRecord", null,
                    AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
            var result = await auditSearchService.SearchAsync(
                authorization.StaffContext!.OrganizationId, branchId, query, cancellationToken);
            await WriteAuditAsync(auditRecordWriter, authorization.StaffContext.OrganizationId, branchId,
                authorization.StaffContext.StaffUserId, AuditActionNames.ViewAudit, "AuditRecord", null,
                AuditOutcome.Succeeded,
                new { Count = result.Records.Count, result.Limit, action, outcome, targetType, fromUtc, toUtc },
                cancellationToken);
            return Results.Ok(result);
        });

        organizations.MapGet("audit", async (
            Guid organizationId,
            string? action,
            string? outcome,
            string? targetType,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            Guid? actorStaffUserId,
            long? minAmount,
            long? maxAmount,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IAuditSearchService auditSearchService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewOrganizationAudit);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
            var result = await auditSearchService.SearchOrganizationAsync(organizationId, query, cancellationToken);
            await WriteAuditAsync(auditRecordWriter, organizationId, Guid.Empty,
                authorization.StaffContext.StaffUserId, AuditActionNames.ViewAudit, "AuditRecord", null,
                AuditOutcome.Succeeded,
                new { Scope = "organization", Count = result.Records.Count, result.Limit, action, outcome, targetType, fromUtc, toUtc },
                cancellationToken);
            return Results.Ok(result);
        }).RequireOrganizationDomain()
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewOrganizationAudit);
    }
}
