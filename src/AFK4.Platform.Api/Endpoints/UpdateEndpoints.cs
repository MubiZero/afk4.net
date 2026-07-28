using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Tenants;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class UpdateEndpoints
{
    public static void MapUpdateEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder organizations)
    {
        organizations.MapPost("branches/{branchId:guid}/updates/packages", async (
            Guid branchId,
            CreateUpdatePackageRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageUpdatePackages,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.RegisterUpdatePackage,
                    "UpdatePackage",
                    null,
                    AuditOutcome.Denied,
                    new { request.Component, request.Version, request.Channel, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await updateService.RegisterPackageAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToUpdateHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.RegisterUpdatePackage,
                "UpdatePackage",
                result.Response!.UpdatePackageId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response.Component, result.Response.Version, result.Response.Channel },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        organizations.MapPost("branches/{branchId:guid}/updates/packages/{packageId:guid}/state", async (
            Guid branchId,
            Guid packageId,
            UpdatePackageStateChangeRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageUpdatePackages,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ChangeUpdatePackageState,
                    "UpdatePackage",
                    packageId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.State, request.Reason, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await updateService.ChangePackageStateAsync(
                authorization.StaffContext.OrganizationId,
                branchId,
                packageId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToUpdateHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ChangeUpdatePackageState,
                "UpdatePackage",
                packageId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response!.State, request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        organizations.MapPost("branches/{branchId:guid}/updates/rollouts", async (
            Guid branchId,
            CreateUpdateRolloutRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageUpdateRollouts,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreateUpdateRollout,
                    "UpdateRollout",
                    null,
                    AuditOutcome.Denied,
                    new { request.UpdatePackageId, request.Channel, request.TargetKind, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await updateService.CreateRolloutAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToUpdateHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateUpdateRollout,
                "UpdateRollout",
                result.Response!.UpdateRolloutId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response.UpdatePackageId, result.Response.Channel, result.Response.TargetKind },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        organizations.MapPost("branches/{branchId:guid}/updates/rollouts/{rolloutId:guid}/state", async (
            Guid branchId,
            Guid rolloutId,
            UpdateRolloutStateChangeRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageUpdateRollouts,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ChangeUpdateRolloutState,
                    "UpdateRollout",
                    rolloutId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.State, request.Reason, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await updateService.ChangeRolloutStateAsync(
                authorization.StaffContext.OrganizationId,
                branchId,
                rolloutId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToUpdateHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ChangeUpdateRolloutState,
                "UpdateRollout",
                rolloutId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response!.State, request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        organizations.MapGet("branches/{branchId:guid}/updates/rollouts", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewUpdateStatus,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewUpdateRollout,
                    "UpdateRollout",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await updateService.ListRolloutStatusesAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToUpdateHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewUpdateRollout,
                "UpdateRollout",
                null,
                AuditOutcome.Succeeded,
                new { Count = result.Response!.Count },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        organizations.MapGet("branches/{branchId:guid}/updates/rollouts/{rolloutId:guid}", async (
            Guid branchId,
            Guid rolloutId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewUpdateStatus,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewUpdateRollout,
                    "UpdateRollout",
                    rolloutId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await updateService.GetRolloutAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                rolloutId,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToUpdateHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewUpdateRollout,
                "UpdateRollout",
                rolloutId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response!.State },
                cancellationToken);

            return Results.Ok(result.Response);
        });

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
                branchId,
                OrganizationPermissionNames.ViewAudit,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewAudit,
                    "AuditRecord",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
            var result = await auditSearchService.SearchAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewAudit,
                "AuditRecord",
                null,
                AuditOutcome.Succeeded,
                new
                {
                    Count = result.Records.Count,
                    result.Limit,
                    action,
                    outcome,
                    targetType,
                    fromUtc,
                    toUtc
                },
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
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    Guid.Empty,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewAudit,
                    "AuditRecord",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (authorization.IsAuthenticated && organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
            var result = await auditSearchService.SearchOrganizationAsync(
                organizationId,
                query,
                cancellationToken);

            if (support is null)
            {
                await WriteAuditAsync(
                    auditRecordWriter, organizationId, Guid.Empty,
                    authorization.StaffContext!.StaffUserId, AuditActionNames.ViewAudit,
                    "AuditRecord", null, AuditOutcome.Succeeded,
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

        app.MapPost("/api/devices/{deviceId:guid}/updates/check", async (
            Guid deviceId,
            DeviceUpdateCheckRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IUpdateService updateService,
            ITenantStatusGuard tenantStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            var result = await updateService.CheckForUpdatesAsync(request, cancellationToken);

            return ToUpdateHttpResult(result);
        });

        app.MapPost("/api/devices/{deviceId:guid}/updates/status", async (
            Guid deviceId,
            DeviceUpdateStatusReportRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IUpdateService updateService,
            ITenantStatusGuard tenantStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
            {
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            }

            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspendedCheck = await tenantStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspendedCheck is not null)
            {
                return suspendedCheck;
            }

            var result = await updateService.ReportStatusAsync(request, cancellationToken);

            return ToUpdateHttpResult(result);
        });

        app.MapHub<DeviceHub>("/hubs/devices");

    }
}
