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
using AFK4.Platform.Api.Identity.OwnerCodes;
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

internal static class OwnerCodeEndpoints
{
    public static void MapOwnerCodeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/staff/me/owner-code", async (
            StaffAuthorizationService authorizationService,
            IOwnerCodeService ownerCodeService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageOwnerCode);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var summary = await ownerCodeService.GetActiveSummaryAsync(
                authorization.StaffContext!.StaffUserId,
                cancellationToken);

            if (summary is null)
            {
                return Results.NoContent();
            }

            return Results.Ok(new OwnerCodeSummaryResponse(
                summary.CodeSuffix,
                summary.ExpiresAtUtc,
                summary.LastUsedAtUtc,
                summary.FailedAttemptCount));
        });

        app.MapPost("/api/staff/me/owner-code/generate", async (
            StaffAuthorizationService authorizationService,
            IOwnerCodeService ownerCodeService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageOwnerCode);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteOwnerCodeAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext?.OrganizationId,
                    authorization.StaffContext?.StaffUserId,
                    AuditActionNames.GenerateOwnerCode,
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staffContext = authorization.StaffContext!;
            var result = await ownerCodeService.GenerateAsync(staffContext.StaffUserId, cancellationToken);

            if (!result.Succeeded)
            {
                await WriteOwnerCodeAuditAsync(
                    auditRecordWriter,
                    staffContext.OrganizationId,
                    staffContext.StaffUserId,
                    AuditActionNames.GenerateOwnerCode,
                    null,
                    AuditOutcome.Denied,
                    new { Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    OwnerCodeOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    OwnerCodeOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var issued = result.Value!;
            await WriteOwnerCodeAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                staffContext.StaffUserId,
                AuditActionNames.GenerateOwnerCode,
                issued.CodeSuffix,
                AuditOutcome.Succeeded,
                new { issued.CodeSuffix, issued.ExpiresAtUtc },
                cancellationToken);

            return Results.Ok(new OwnerCodeIssuedResponse(
                issued.PlaintextCode,
                issued.CodeSuffix,
                issued.ExpiresAtUtc));
        });

        app.MapPost("/api/staff/me/owner-code/rotate", async (
            RotateOwnerCodeRequest request,
            StaffAuthorizationService authorizationService,
            IOwnerCodeService ownerCodeService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageOwnerCode);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteOwnerCodeAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext?.OrganizationId,
                    authorization.StaffContext?.StaffUserId,
                    AuditActionNames.RotateOwnerCode,
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staffContext = authorization.StaffContext!;
            var result = await ownerCodeService.RotateAsync(
                staffContext.StaffUserId,
                request.Reason,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WriteOwnerCodeAuditAsync(
                    auditRecordWriter,
                    staffContext.OrganizationId,
                    staffContext.StaffUserId,
                    AuditActionNames.RotateOwnerCode,
                    null,
                    AuditOutcome.Denied,
                    new { Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    OwnerCodeOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    OwnerCodeOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var issued = result.Value!;
            await WriteOwnerCodeAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                staffContext.StaffUserId,
                AuditActionNames.RotateOwnerCode,
                issued.CodeSuffix,
                AuditOutcome.Succeeded,
                new { issued.CodeSuffix, issued.ExpiresAtUtc, request.Reason },
                cancellationToken);

            return Results.Ok(new OwnerCodeIssuedResponse(
                issued.PlaintextCode,
                issued.CodeSuffix,
                issued.ExpiresAtUtc));
        });

    }
}
