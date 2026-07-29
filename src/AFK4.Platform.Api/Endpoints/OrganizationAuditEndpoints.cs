using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Support;
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
            HttpContext httpContext,
            StaffAuthorizationService authorizationService,
            IPlatformAdminContextAccessor platformContextAccessor,
            PlatformSupportAccessGrantService supportAccessService,
            IAuditRecordWriter auditRecordWriter,
            IAuditSearchService auditSearchService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewOrganizationAudit);
            PlatformSupportContext? support = null;
            if (!authorization.IsAuthenticated)
            {
                support = await supportAccessService.ValidateAsync(
                    httpContext, organizationId, OrganizationPermissionNames.ViewOrganizationAudit,
                    platformContextAccessor, cancellationToken);
                if (platformContextAccessor.Current is null) return Results.Unauthorized();
                if (support is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            if (authorization.IsAuthenticated && !authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (authorization.IsAuthenticated && organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
            var result = await auditSearchService.SearchOrganizationAsync(organizationId, query, cancellationToken);
            if (support is null)
            {
                await WriteAuditAsync(auditRecordWriter, organizationId, Guid.Empty,
                    authorization.StaffContext!.StaffUserId, AuditActionNames.ViewAudit, "AuditRecord", null,
                    AuditOutcome.Succeeded,
                    new { Scope = "organization", Count = result.Records.Count, result.Limit, action, outcome, targetType, fromUtc, toUtc },
                    cancellationToken);
            }
            else
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    organizationId, null, null, AuditActionNames.UsePlatformSupportAccess, "AuditRecord", null,
                    AuditOutcome.Succeeded, "PlatformApi",
                    JsonSerializer.Serialize(new { support.GrantId, support.Reason, Permission = support.Permission, Count = result.Records.Count }))
                { ActorPlatformAdminUserId = support.PlatformAdminUserId }, cancellationToken);
            }
            return Results.Ok(result);
        }).RequireOrganizationDomain()
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewOrganizationAudit);
    }
}
