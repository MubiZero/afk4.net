using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Payments.DcGate;
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

internal static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/sessions/start", async (
            Guid branchId,
            StartGuestSessionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.StartSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.StartSession,
                    "Session",
                    null,
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.SeatId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await sessionCommandService.StartGuestSessionAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken,
                actorCanApproveComp: authorization.StaffContext.Permissions.Contains(StaffPermissionNames.ApproveMoneyAction));

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            // §5.4: a comp (free session) is audited as a first-class session.comp with its reason and its
            // assessed value, so the owner summary / Review screen can surface free sessions in money terms.
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                request.IsComp ? AuditActionNames.SessionComp : AuditActionNames.StartSession,
                "Session",
                result.Response!.Session.SessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                request.IsComp
                    ? JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes, request.CompReason, CompValueMinorUnits = result.Response.CompValueMinorUnits })
                    : JsonSerializer.Serialize(new { request.SeatId, request.DurationMinutes }))
            {
                AmountMinorUnits = request.IsComp ? result.Response.CompValueMinorUnits : null
            },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/extend", async (
            Guid sessionId,
            ExtendSessionRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                StaffPermissionNames.ExtendSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    session.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ExtendSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.ExtendSessionAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ExtendSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    request.AdditionalMinutes,
                    request.TariffRuleVersionId
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/transfer", async (
            Guid sessionId,
            TransferSessionRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                StaffPermissionNames.TransferSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    session.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.TransferSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.TargetSeatId,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.TransferSessionAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.TransferSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    request.TargetSeatId
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/end", async (
            Guid sessionId,
            EndSessionRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCommandService sessionCommandService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                StaffPermissionNames.EndSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    session.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.EndSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        request.Reason,
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCommandService.EndSessionAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.EndSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    request.Reason
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/sessions/{sessionId:guid}/checkout", async (
            Guid sessionId,
            SessionCheckoutRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            ISessionCheckoutService sessionCheckoutService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                StaffPermissionNames.EndSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    authorization.StaffContext!.OrganizationId,
                    session.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CheckoutSession,
                    "Session",
                    sessionId.ToString("D"),
                    AuditOutcome.Denied,
                    "PlatformApi",
                    JsonSerializer.Serialize(new
                    {
                        authorization.DenialReason
                    })),
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCheckoutService.CheckoutAsync(
                sessionId,
                authorization.StaffContext!.StaffUserId,
                request,
                cancellationToken);

            if (result.Conflict)
            {
                return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion });
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                authorization.StaffContext.OrganizationId,
                session.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CheckoutSession,
                "Session",
                sessionId.ToString("D"),
                AuditOutcome.Succeeded,
                "PlatformApi",
                JsonSerializer.Serialize(new
                {
                    GrandTotal = result.Response!.GrandTotal.MinorUnits,
                    result.Response.GrandTotal.CurrencyCode,
                    PaymentParts = result.Response.Payments.Count
                })),
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("/api/sessions/{sessionId:guid}/checkout/quote", async (
            Guid sessionId,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            ISessionCheckoutService sessionCheckoutService,
            CancellationToken cancellationToken) =>
        {
            var session = await dbContext.Sessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

            if (session is null)
            {
                return Results.NotFound();
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                session.BranchId,
                StaffPermissionNames.EndSession,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await sessionCheckoutService.QuoteAsync(
                sessionId,
                authorization.StaffContext!.OrganizationId,
                cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound(new { Error = result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.Ok(result.Response);
        });

    }
}
