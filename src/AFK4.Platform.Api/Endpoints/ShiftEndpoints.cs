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

internal static class ShiftEndpoints
{
    public static void MapShiftEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/shifts/open", async (
            Guid branchId,
            OpenShiftRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.OpenShift,
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
                    AuditActionNames.OpenShift,
                    "Shift",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await shiftService.OpenShiftAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.OpenShift,
                "Shift",
                result.Response!.ShiftId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.StartingCash },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("/api/branches/{branchId:guid}/shifts/current", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ViewShift,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await shiftService.GetCurrentShiftAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            return result.Response is null
                ? Results.NotFound()
                : Results.Ok(result.Response);
        });

        app.MapPost("/api/shifts/{shiftId:guid}/cash-movements", async (
            Guid shiftId,
            RecordCashMovementRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var shift = await LoadShiftScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                shiftId,
                StaffPermissionNames.ManageShiftCash,
                cancellationToken);
            if (shift.Result is not null)
            {
                return shift.Result;
            }

            var authorization = shift.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.RecordCashMovement,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await shiftService.RecordCashMovementAsync(
                shiftId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                shift.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.RecordCashMovement,
                "CashMovement",
                result.Response!.CashMovementId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.MovementType, request.Amount },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("/api/branches/{branchId:guid}/shifts/revenue/current", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.ViewReports, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await reportService.GetCurrentShiftRevenueAsync(
                authorization.StaffContext!.OrganizationId, branchId, cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapGet("/api/branches/{branchId:guid}/shifts/revenue", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.ViewReports, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit);
            var result = await reportService.GetShiftRevenueAsync(
                authorization.StaffContext!.OrganizationId, branchId, query, cancellationToken);

            return Results.Ok(result);
        });

        app.MapPost("/api/shifts/{shiftId:guid}/close", async (
            Guid shiftId,
            CloseShiftRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var shift = await LoadShiftScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                shiftId,
                StaffPermissionNames.CloseShift,
                cancellationToken);
            if (shift.Result is not null)
            {
                return shift.Result;
            }

            var authorization = shift.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CloseShift,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await shiftService.CloseShiftAsync(
                shiftId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                shift.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CloseShift,
                "Shift",
                shiftId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.CountedCash, result.Response!.Difference },
                cancellationToken);

            // Anti-fraud §5.7: record the manager sign-off as its own audit fact when a discrepancy was cleared.
            if (result.Response.ManagerSignOffStaffUserId is { } signOffStaffUserId)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ShiftSignOff,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Succeeded,
                    new { SignOffStaffUserId = signOffStaffUserId, result.Response.Difference, request.SignOffReason },
                    cancellationToken);
            }

            return Results.Ok(result.Response);
        });

    }
}
