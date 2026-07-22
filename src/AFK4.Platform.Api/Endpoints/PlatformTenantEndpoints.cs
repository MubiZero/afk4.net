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

internal static class PlatformTenantEndpoints
{
    public static void MapPlatformTenantEndpoints(this WebApplication app)
    {
        app.MapPost("/api/platform/auth/sign-in", async (
            PlatformAdminSignInRequest request,
            IPlatformAdminCredentialService credentialService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var response = await credentialService.SignInAsync(request, cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: Guid.Empty,
                BranchId: null,
                ActorStaffUserId: null,
                Action: AuditActionNames.PlatformAdminSignIn,
                TargetType: "PlatformAdminUser",
                TargetId: response?.PlatformAdminId.ToString("D") ?? request.UserName,
                Outcome: response is null ? AuditOutcome.Denied : AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(new { request.UserName })),
                cancellationToken);

            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        app.MapPost("/api/platform/auth/refresh", async (
            PlatformAdminRefreshTokenRequest request,
            IPlatformAdminTokenService tokenService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var response = await tokenService.RefreshAsync(request, cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: Guid.Empty,
                BranchId: null,
                ActorStaffUserId: null,
                Action: AuditActionNames.PlatformAdminRefresh,
                TargetType: "PlatformAdminUser",
                TargetId: response?.PlatformAdminId.ToString("D"),
                Outcome: response is null ? AuditOutcome.Denied : AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: "{}"),
                cancellationToken);

            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        app.MapPost("/api/platform/auth/sign-out", async (
            PlatformAdminSignOutRequest request,
            IPlatformAdminTokenService tokenService,
            PlatformAdminAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireAuthenticated();
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var revoked = await tokenService.RevokeAsync(request, cancellationToken);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: Guid.Empty,
                BranchId: null,
                ActorStaffUserId: null,
                Action: AuditActionNames.PlatformAdminSignOut,
                TargetType: "PlatformAdminUser",
                TargetId: authorization.PlatformAdminContext!.PlatformAdminUserId.ToString("D"),
                Outcome: revoked ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                SourceApp: "PlatformApi",
                DetailsJson: "{}"),
                cancellationToken);

            return revoked ? Results.NoContent() : Results.Unauthorized();
        });

        app.MapPost("/api/platform/tenants", async (
            CreateTenantRequest request,
            HttpContext httpContext,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IPlatformIdempotencyStore idempotencyStore,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.CreateTenant);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.CreateTenant,
                    targetType: "Tenant",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.OrganizationSlug, request.BranchSlug, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var requestHash = IdempotencyKeyHelper.HashRequest(request);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                if (idempotencyKey.Length > 128)
                {
                    return Results.BadRequest(new { Error = "Idempotency-Key must be at most 128 characters." });
                }

                var prior = await idempotencyStore.TryReadAsync(
                    scope: "platform.tenants.create",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    cancellationToken);
                if (prior.RequestHashMismatch)
                {
                    return Results.Json(
                        new { Error = "Idempotency-Key was reused with a different request body." },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }
                if (prior.Stored is not null)
                {
                    httpContext.Response.Headers["Idempotency-Replayed"] = "true";
                    return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
                }
            }

            var result = await tenantService.CreateAsync(
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.CreateTenant,
                    targetType: "Tenant",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.OrganizationSlug, request.BranchSlug, Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var created = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: created.Tenant.OrganizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.CreateTenant,
                targetType: "Tenant",
                targetId: created.Tenant.OrganizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new
                {
                    created.Tenant.Slug,
                    BranchSlug = created.Tenant.Branches.First().Slug,
                    created.Tenant.PlanCode,
                    created.Tenant.SubscriptionStatus,
                    OwnerInviteId = created.OwnerInvite.OwnerInviteId
                },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var responseBody = JsonSerializer.Serialize(created, IdempotencyKeyHelper.JsonOptions);
                await idempotencyStore.WriteAsync(
                    scope: "platform.tenants.create",
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash,
                    platformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    statusCode: StatusCodes.Status200OK,
                    responseBody: responseBody,
                    retention: TimeSpan.FromHours(24),
                    cancellationToken);
            }

            return Results.Ok(created);
        });

        app.MapGet("/api/platform/tenants", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewTenants);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewTenant,
                    targetType: "Tenant",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var summaries = await tenantService.ListAsync(cancellationToken);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewTenant,
                targetType: "Tenant",
                targetId: null,
                outcome: AuditOutcome.Succeeded,
                details: new { Count = summaries.Count },
                cancellationToken);

            return Results.Ok(summaries);
        });

        app.MapGet("/api/platform/tenants/{organizationId:guid}", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewTenants);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewTenant,
                    targetType: "Tenant",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var detail = await tenantService.GetAsync(organizationId, cancellationToken);
            if (detail is null)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewTenant,
                    targetType: "Tenant",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = "Tenant was not found." },
                    cancellationToken);
                return Results.NotFound(new { Error = "Tenant was not found." });
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewTenant,
                targetType: "Tenant",
                targetId: organizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { detail.Slug, BranchCount = detail.Branches.Count },
                cancellationToken);

            return Results.Ok(detail);
        });

        app.MapGet("/api/platform/tenants/{organizationId:guid}/owner-invites", async (
            Guid organizationId,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOwnerInvites,
                    targetType: "OwnerInvite",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await tenantService.ListOwnerInvitesAsync(organizationId, cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewOwnerInvites,
                    targetType: "OwnerInvite",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var invites = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewOwnerInvites,
                targetType: "OwnerInvite",
                targetId: null,
                outcome: AuditOutcome.Succeeded,
                details: new { Count = invites.Count },
                cancellationToken);

            return Results.Ok(invites);
        });

        app.MapPost("/api/platform/tenants/{organizationId:guid}/owner-invites", async (
            Guid organizationId,
            CreateOwnerInviteRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.CreateOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.BranchId, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await tenantService.CreateOrRotateOwnerInviteAsync(
                organizationId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.CreateOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.BranchId, Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var invite = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.CreateOwnerInvite,
                targetType: "OwnerInvite",
                targetId: invite.OwnerInviteId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new
                {
                    invite.BranchId,
                    invite.OwnerUserName,
                    invite.ExpiresAtUtc
                },
                cancellationToken);

            return Results.Ok(invite);
        });

        app.MapPost("/api/platform/owner-invites/{ownerInviteId:guid}/resend", async (
            Guid ownerInviteId,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var organizationId = await dbContext.OwnerInvites
                .AsNoTracking()
                .Where(invite => invite.OwnerInviteId == ownerInviteId)
                .Select(invite => (Guid?)invite.OrganizationId)
                .SingleOrDefaultAsync(cancellationToken) ?? Guid.Empty;

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ResendOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: ownerInviteId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await tenantService.ResendOwnerInviteAsync(ownerInviteId, cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ResendOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: ownerInviteId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ResendOwnerInvite,
                targetType: "OwnerInvite",
                targetId: ownerInviteId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { result.Value!.BranchId },
                cancellationToken);

            return Results.Ok(result.Value);
        });

        app.MapPost("/api/platform/owner-invites/{ownerInviteId:guid}/revoke", async (
            Guid ownerInviteId,
            RevokeOwnerInviteRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageOwnerInvites);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var orgIdForAudit = await dbContext.OwnerInvites
                .AsNoTracking()
                .Where(invite => invite.OwnerInviteId == ownerInviteId)
                .Select(invite => (Guid?)invite.OrganizationId)
                .SingleOrDefaultAsync(cancellationToken) ?? Guid.Empty;

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: orgIdForAudit,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.RevokeOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: ownerInviteId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await tenantService.RevokeOwnerInviteAsync(
                ownerInviteId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: orgIdForAudit,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.RevokeOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: ownerInviteId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var invite = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: invite.OrganizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.RevokeOwnerInvite,
                targetType: "OwnerInvite",
                targetId: invite.OwnerInviteId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { invite.OwnerInviteId, Reason = request.Reason },
                cancellationToken);

            return Results.Ok(invite);
        });

        app.MapPost("/api/platform/owner-invites/accept", async (
            AcceptOwnerInviteRequest request,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantService.AcceptOwnerInviteAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: null,
                    action: AuditActionNames.AcceptOwnerInvite,
                    targetType: "OwnerInvite",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.UserName, Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var signIn = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: signIn.OrganizationId,
                actorPlatformAdminUserId: null,
                action: AuditActionNames.AcceptOwnerInvite,
                targetType: "StaffUser",
                targetId: signIn.StaffUserId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { signIn.DisplayName, request.UserName },
                cancellationToken);

            return Results.Ok(signIn);
        });

        app.MapPost("/api/operator-connections/resolve", async (
            ResolveOperatorConnectionRequest request,
            IOperatorConnectionResolver resolver,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var result = await resolver.ResolveAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: null,
                    action: AuditActionNames.ResolveOperatorConnection,
                    targetType: "OperatorConnection",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new
                    {
                        HasSlugPair = !string.IsNullOrWhiteSpace(request.OrganizationSlug)
                            || !string.IsNullOrWhiteSpace(request.BranchSlug),
                        HasSetupCode = !string.IsNullOrWhiteSpace(request.SetupCode),
                        Error = result.Error
                    },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var resolution = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: resolution.OrganizationId,
                actorPlatformAdminUserId: null,
                action: AuditActionNames.ResolveOperatorConnection,
                targetType: "OperatorConnection",
                targetId: resolution.BranchId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new
                {
                    resolution.Source,
                    resolution.OrganizationSlug,
                    resolution.BranchSlug,
                    resolution.OrganizationStatus
                },
                cancellationToken);

            return Results.Ok(resolution);
        });

        app.MapPatch("/api/platform/tenants/{organizationId:guid}/status", async (
            Guid organizationId,
            UpdateTenantStatusRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UpdateTenantStatus);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.UpdateTenantStatus,
                    targetType: "Tenant",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { request.Status, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var previousStatus = await dbContext.Organizations
                .AsNoTracking()
                .Where(org => org.OrganizationId == organizationId)
                .Select(org => new { org.Status, org.StatusReason })
                .SingleOrDefaultAsync(cancellationToken);

            var result = await tenantService.UpdateStatusAsync(
                organizationId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.UpdateTenantStatus,
                    targetType: "Tenant",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { request.Status, Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var detail = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.UpdateTenantStatus,
                targetType: "Tenant",
                targetId: organizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new
                {
                    PreviousStatus = previousStatus?.Status,
                    PreviousReason = previousStatus?.StatusReason,
                    NewStatus = detail.Status,
                    NewReason = detail.StatusReason
                },
                cancellationToken);

            return Results.Ok(detail);
        });

        app.MapPatch("/api/platform/tenants/{organizationId:guid}/limits", async (
            Guid organizationId,
            UpdateTenantLimitsRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformTenantService tenantService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.UpdateTenantLimits);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.UpdateTenantLimits,
                    targetType: "Tenant",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await tenantService.UpdateLimitsAsync(
                organizationId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.UpdateTenantLimits,
                    targetType: "Tenant",
                    targetId: organizationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformTenantOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformTenantOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var detail = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.UpdateTenantLimits,
                targetType: "Tenant",
                targetId: organizationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new
                {
                    detail.Limits.MaxBranches,
                    detail.Limits.MaxDevicesPerBranch,
                    detail.Limits.MaxConcurrentSessions,
                    detail.Limits.MaxStaffUsersPerBranch
                },
                cancellationToken);

            return Results.Ok(detail);
        });

    }
}
