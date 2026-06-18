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

internal static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branches/{branchId:guid}/reservations", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            string? state,
            string? source,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ViewReservations,
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
                    AuditActionNames.ViewReservations,
                    "Reservation",
                    null,
                    AuditOutcome.Denied,
                    new { fromUtc, toUtc, state, source, limit, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await reservationService.SearchAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                new ReservationSearchQuery(fromUtc, toUtc, state, source, limit),
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewReservations,
                "Reservation",
                null,
                AuditOutcome.Succeeded,
                new { fromUtc, toUtc, state, source, limit, ResultCount = result.Reservations.Count },
                cancellationToken);

            return Results.Ok(result);
        });

        app.MapPost("/api/branches/{branchId:guid}/reservations", async (
            Guid branchId,
            CreateReservationRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageReservations,
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
                    AuditActionNames.CreateReservation,
                    "Reservation",
                    null,
                    AuditOutcome.Denied,
                    new { request.CustomerName, request.StartsAtUtc, request.SeatId, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.CreateAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateReservation,
                "Reservation",
                result.Response!.ReservationId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.CustomerName, request.StartsAtUtc, request.SeatId, result.Response.State, result.Response.Source },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        // Group create: several seats booked together as one reservation (drag across timeline rows).
        // All-or-nothing — any conflicting seat returns 409 with the conflict list, nothing is written.
        app.MapPost("/api/branches/{branchId:guid}/reservations/group", async (
            Guid branchId,
            CreateReservationGroupRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageReservations,
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
                    AuditActionNames.CreateReservation,
                    "Reservation",
                    null,
                    AuditOutcome.Denied,
                    new { request.CustomerName, request.StartsAtUtc, SeatCount = request.SeatIds?.Count ?? 0, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.CreateGroupAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            switch (result.Status)
            {
                case ReservationGroupStatus.Invalid:
                    return Results.BadRequest(new { Error = result.Error });
                case ReservationGroupStatus.Conflict:
                    return Results.Json(result.Result, statusCode: StatusCodes.Status409Conflict);
                default:
                    await WriteAuditAsync(
                        auditRecordWriter,
                        authorization.StaffContext.OrganizationId,
                        branchId,
                        authorization.StaffContext.StaffUserId,
                        AuditActionNames.CreateReservation,
                        "Reservation",
                        result.Result!.ReservationGroupId?.ToString("D"),
                        AuditOutcome.Succeeded,
                        new { request.CustomerName, request.StartsAtUtc, SeatCount = result.Result.Reservations.Count, result.Result.ReservationGroupId },
                        cancellationToken);

                    return Results.Ok(result.Result);
            }
        });

        app.MapPatch("/api/reservations/{reservationId:guid}", async (
            Guid reservationId,
            UpdateReservationRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var scoped = await LoadReservationForStaffAsync(
                dbContext,
                staffContextAccessor,
                reservationId,
                cancellationToken);
            if (scoped.Result is not null)
            {
                return scoped.Result;
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                scoped.Reservation!.BranchId,
                StaffPermissionNames.ManageReservations,
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
                    scoped.Reservation.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateReservation,
                    "Reservation",
                    reservationId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.SeatId, request.StartsAtUtc, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.UpdateAsync(
                reservationId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                result.Response!.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateReservation,
                "Reservation",
                reservationId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response.SeatId, result.Response.StartsAtUtc, result.Response.State },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/reservations/{reservationId:guid}/confirm", async (
            Guid reservationId,
            ConfirmReservationRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
            if (scoped.Result is not null)
            {
                return scoped.Result;
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                scoped.Reservation!.BranchId,
                StaffPermissionNames.ManageReservations,
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
                    scoped.Reservation.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ConfirmReservation,
                    "Reservation",
                    reservationId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.ConfirmAsync(
                reservationId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                result.Response!.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ConfirmReservation,
                "Reservation",
                reservationId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response.State },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/reservations/{reservationId:guid}/seat", async (
            Guid reservationId,
            SeatReservationRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
            if (scoped.Result is not null)
            {
                return scoped.Result;
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                scoped.Reservation!.BranchId,
                StaffPermissionNames.ManageReservations,
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
                    scoped.Reservation.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.SeatReservation,
                    "Reservation",
                    reservationId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.SeatAsync(
                reservationId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                result.Response!.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.SeatReservation,
                "Reservation",
                reservationId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response.SeatId, result.Response.State },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/reservations/{reservationId:guid}/cancel", async (
            Guid reservationId,
            CancelReservationRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var scoped = await LoadReservationForStaffAsync(dbContext, staffContextAccessor, reservationId, cancellationToken);
            if (scoped.Result is not null)
            {
                return scoped.Result;
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                scoped.Reservation!.BranchId,
                StaffPermissionNames.ManageReservations,
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
                    scoped.Reservation.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CancelReservation,
                    "Reservation",
                    reservationId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await reservationService.CancelAsync(
                reservationId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);
            if (!result.Succeeded)
            {
                return ToReservationHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                result.Response!.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CancelReservation,
                "Reservation",
                reservationId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.Response.State, request.Reason },
                cancellationToken);

            return Results.Ok(result.Response);
        });

    }
}
