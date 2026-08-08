using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

public static class PlatformFeatureEndpoints
{
    private const int MaxReasonLength = 500;

    public static void MapPlatformFeatureEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/organizations/{organizationId:guid}/features", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            IOrganizationEntitlements entitlements,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizations);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOrganizationFeatures,
                    targetType: "Organization",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationExists = await dbContext.Organizations
                .AsNoTracking()
                .AnyAsync(organization => organization.OrganizationId == organizationId, cancellationToken);
            if (!organizationExists)
            {
                return Results.NotFound(new { Error = "Organization was not found." });
            }

            var states = await entitlements.DescribeAsync(organizationId, cancellationToken);
            return Results.Ok(states);
        });

        app.MapPut("/api/platform/organizations/{organizationId:guid}/features/{featureKey}", async (
            Guid organizationId,
            string featureKey,
            SetFeatureOverrideRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            IOrganizationEntitlements entitlements,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOrganizationFeatures);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.SetOrganizationFeatureOverride,
                    targetType: "OrganizationFeatureOverride",
                    targetId: featureKey,
                    outcome: AuditOutcome.Denied,
                    details: new { FeatureKey = featureKey, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationExists = await dbContext.Organizations
                .AsNoTracking()
                .AnyAsync(organization => organization.OrganizationId == organizationId, cancellationToken);
            if (!organizationExists)
            {
                return Results.NotFound(new { Error = "Organization was not found." });
            }

            var featureExists = await dbContext.PlatformFeatures
                .AsNoTracking()
                .AnyAsync(feature => feature.FeatureKey == featureKey, cancellationToken);
            if (!featureExists)
            {
                return Results.NotFound(new { Error = "Feature was not found." });
            }

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > MaxReasonLength)
            {
                return Results.BadRequest(new { Error = "Reason is required and must be at most 500 characters." });
            }

            var existingOverride = await dbContext.OrganizationFeatureOverrides
                .SingleOrDefaultAsync(
                    featureOverride => featureOverride.OrganizationId == organizationId && featureOverride.FeatureKey == featureKey,
                    cancellationToken);

            var now = DateTimeOffset.UtcNow;
            if (existingOverride is null)
            {
                dbContext.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
                {
                    OrganizationFeatureOverrideId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    FeatureKey = featureKey,
                    IsEnabled = request.IsEnabled,
                    Reason = request.Reason,
                    SetByPlatformAdminUserId = authorization.PlatformAdminContext!.PlatformAdminUserId,
                    SetAtUtc = now
                });
            }
            else
            {
                existingOverride.IsEnabled = request.IsEnabled;
                existingOverride.Reason = request.Reason;
                existingOverride.SetByPlatformAdminUserId = authorization.PlatformAdminContext!.PlatformAdminUserId;
                existingOverride.SetAtUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.SetOrganizationFeatureOverride,
                targetType: "OrganizationFeatureOverride",
                targetId: featureKey,
                outcome: AuditOutcome.Succeeded,
                details: new { FeatureKey = featureKey, request.IsEnabled, request.Reason },
                cancellationToken);

            var states = await entitlements.DescribeAsync(organizationId, cancellationToken);
            return Results.Ok(states);
        });

        app.MapDelete("/api/platform/organizations/{organizationId:guid}/features/{featureKey}", async (
            Guid organizationId,
            string featureKey,
            PlatformAdminAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            IOrganizationEntitlements entitlements,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOrganizationFeatures);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ClearOrganizationFeatureOverride,
                    targetType: "OrganizationFeatureOverride",
                    targetId: featureKey,
                    outcome: AuditOutcome.Denied,
                    details: new { FeatureKey = featureKey, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationExists = await dbContext.Organizations
                .AsNoTracking()
                .AnyAsync(organization => organization.OrganizationId == organizationId, cancellationToken);
            if (!organizationExists)
            {
                return Results.NotFound(new { Error = "Organization was not found." });
            }

            var featureExists = await dbContext.PlatformFeatures
                .AsNoTracking()
                .AnyAsync(feature => feature.FeatureKey == featureKey, cancellationToken);
            if (!featureExists)
            {
                return Results.NotFound(new { Error = "Feature was not found." });
            }

            var existingOverride = await dbContext.OrganizationFeatureOverrides
                .SingleOrDefaultAsync(
                    featureOverride => featureOverride.OrganizationId == organizationId && featureOverride.FeatureKey == featureKey,
                    cancellationToken);
            if (existingOverride is not null)
            {
                dbContext.OrganizationFeatureOverrides.Remove(existingOverride);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ClearOrganizationFeatureOverride,
                targetType: "OrganizationFeatureOverride",
                targetId: featureKey,
                outcome: AuditOutcome.Succeeded,
                details: new { FeatureKey = featureKey },
                cancellationToken);

            var states = await entitlements.DescribeAsync(organizationId, cancellationToken);
            return Results.Ok(states);
        });
    }
}
