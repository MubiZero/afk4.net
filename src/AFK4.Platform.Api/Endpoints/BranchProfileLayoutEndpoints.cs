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

internal static class BranchProfileLayoutEndpoints
{
    public static void MapBranchProfileLayoutEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branches/{branchId:guid}/profile", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.ViewBranchProfile,
                    "Branch",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            var branch = await dbContext.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                    cancellationToken);

            if (branch is null)
            {
                return Results.NotFound();
            }

            var response = ToBranchProfileDto(branch);
            await WriteAuditAsync(
                auditRecordWriter,
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewBranchProfile,
                "Branch",
                branchId.ToString("D"),
                AuditOutcome.Succeeded,
                new { branch.Name, branch.City },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPatch("/api/branches/{branchId:guid}/profile", async (
            Guid branchId,
            UpdateBranchProfileRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.UpdateBranchProfile,
                    "Branch",
                    branchId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Name, request.City, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateUpdateBranchProfileRequest(request);
            if (validation is not null)
            {
                return Results.BadRequest(new { Error = validation });
            }

            var branch = await dbContext.Branches
                .SingleOrDefaultAsync(
                    candidate => candidate.OrganizationId == request.OrganizationId && candidate.BranchId == branchId,
                    cancellationToken);

            if (branch is null)
            {
                return Results.NotFound();
            }

            branch.Name = request.Name.Trim();
            branch.City = request.City.Trim();
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToBranchProfileDto(branch);
            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateBranchProfile,
                "Branch",
                branchId.ToString("D"),
                AuditOutcome.Succeeded,
                new { branch.Name, branch.City },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapGet("/api/branches/{branchId:guid}/layout/zones", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.ViewLayout,
                    "Layout",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var organizationId = authorization.StaffContext!.OrganizationId;
            var zones = await dbContext.Zones
                .AsNoTracking()
                .Where(zone => zone.OrganizationId == organizationId && zone.BranchId == branchId)
                .OrderBy(zone => zone.SortOrder)
                .ThenBy(zone => zone.Name)
                .ToListAsync(cancellationToken);
            var zoneIds = zones.Select(zone => zone.ZoneId).ToHashSet();
            var seats = await dbContext.Seats
                .AsNoTracking()
                .Where(seat =>
                    seat.OrganizationId == organizationId &&
                    seat.BranchId == branchId &&
                    zoneIds.Contains(seat.ZoneId))
                .OrderBy(seat => seat.SortOrder)
                .ThenBy(seat => seat.Name)
                .ToListAsync(cancellationToken);
            var seatsByZoneId = seats
                .GroupBy(seat => seat.ZoneId)
                .ToDictionary(group => group.Key, group => group.ToList() as IReadOnlyList<SeatEntity>);
            var response = zones
                .Select(zone => ToZoneDto(zone, seatsByZoneId.GetValueOrDefault(zone.ZoneId) ?? []))
                .ToList();

            await WriteAuditAsync(
                auditRecordWriter,
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewLayout,
                "Layout",
                null,
                AuditOutcome.Succeeded,
                new { ZoneCount = response.Count, SeatCount = seats.Count },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPost("/api/branches/{branchId:guid}/layout/zones", async (
            Guid branchId,
            CreateZoneRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.CreateZone,
                    "Zone",
                    null,
                    AuditOutcome.Denied,
                    new { request.Name, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { Error = "Zone name is required." });
            }

            var normalizedName = request.Name.Trim().ToUpperInvariant();
            var zone = await dbContext.Zones.SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.Name.ToUpper() == normalizedName,
                cancellationToken);

            if (zone is null)
            {
                zone = new ZoneEntity
                {
                    ZoneId = Guid.NewGuid(),
                    OrganizationId = request.OrganizationId,
                    BranchId = branchId,
                    Name = request.Name.Trim(),
                    SortOrder = request.SortOrder,
                    CreatedAtUtc = timeProvider.GetUtcNow()
                };
                dbContext.Zones.Add(zone);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var response = ToZoneDto(zone, []);
            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateZone,
                "Zone",
                zone.ZoneId.ToString("D"),
                AuditOutcome.Succeeded,
                new { zone.Name, zone.SortOrder },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPatch("/api/branches/{branchId:guid}/layout/zones/{zoneId:guid}", async (
            Guid branchId,
            Guid zoneId,
            UpdateZoneRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.UpdateZone,
                    "Zone",
                    zoneId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Name, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { Error = "Zone name is required." });
            }

            var zone = await dbContext.Zones.SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ZoneId == zoneId,
                cancellationToken);

            if (zone is null)
            {
                return Results.NotFound(new { Error = "Zone was not found." });
            }

            var trimmedName = request.Name.Trim();
            var normalizedName = trimmedName.ToUpperInvariant();
            var duplicateName = await dbContext.Zones.AnyAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ZoneId != zoneId &&
                    candidate.Name.ToUpper() == normalizedName,
                cancellationToken);

            if (duplicateName)
            {
                return Results.Conflict(new { Error = "Zone name already exists." });
            }

            zone.Name = trimmedName;
            zone.SortOrder = request.SortOrder;
            await dbContext.SaveChangesAsync(cancellationToken);

            var seats = await dbContext.Seats
                .AsNoTracking()
                .Where(seat =>
                    seat.OrganizationId == request.OrganizationId &&
                    seat.BranchId == branchId &&
                    seat.ZoneId == zoneId)
                .OrderBy(seat => seat.SortOrder)
                .ThenBy(seat => seat.Name)
                .ToListAsync(cancellationToken);
            var response = ToZoneDto(zone, seats);

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateZone,
                "Zone",
                zone.ZoneId.ToString("D"),
                AuditOutcome.Succeeded,
                new { zone.Name, zone.SortOrder },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapDelete("/api/branches/{branchId:guid}/layout/zones/{zoneId:guid}", async (
            Guid branchId,
            Guid zoneId,
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.DeleteZone,
                    "Zone",
                    zoneId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (organizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var zone = await dbContext.Zones.SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ZoneId == zoneId,
                cancellationToken);

            if (zone is null)
            {
                return Results.NotFound(new { Error = "Zone was not found." });
            }

            var hasSeats = await dbContext.Seats.AnyAsync(
                seat =>
                    seat.OrganizationId == organizationId &&
                    seat.BranchId == branchId &&
                    seat.ZoneId == zoneId,
                cancellationToken);

            if (hasSeats)
            {
                return Results.Conflict(new { Error = "Zone must be empty before deletion." });
            }

            var zoneName = zone.Name;
            var sortOrder = zone.SortOrder;
            dbContext.Zones.Remove(zone);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.DeleteZone,
                "Zone",
                zoneId.ToString("D"),
                AuditOutcome.Succeeded,
                new { Name = zoneName, SortOrder = sortOrder },
                cancellationToken);

            return Results.NoContent();
        });

        app.MapPost("/api/branches/{branchId:guid}/layout/seats", async (
            Guid branchId,
            CreateSeatRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.CreateSeat,
                    "Seat",
                    null,
                    AuditOutcome.Denied,
                    new { request.ZoneId, request.Name, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (request.ZoneId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "ZoneId is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { Error = "Seat name is required." });
            }

            var zoneExists = await dbContext.Zones.AnyAsync(
                zone =>
                    zone.OrganizationId == request.OrganizationId &&
                    zone.BranchId == branchId &&
                    zone.ZoneId == request.ZoneId,
                cancellationToken);
            if (!zoneExists)
            {
                return Results.NotFound(new { Error = "Zone was not found." });
            }

            var normalizedName = request.Name.Trim().ToUpperInvariant();
            var seat = await dbContext.Seats.SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ZoneId == request.ZoneId &&
                    candidate.Name.ToUpper() == normalizedName,
                cancellationToken);

            if (seat is null)
            {
                seat = new SeatEntity
                {
                    SeatId = Guid.NewGuid(),
                    OrganizationId = request.OrganizationId,
                    BranchId = branchId,
                    ZoneId = request.ZoneId,
                    Name = request.Name.Trim(),
                    SortOrder = request.SortOrder,
                    CreatedAtUtc = timeProvider.GetUtcNow()
                };
                dbContext.Seats.Add(seat);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var response = ToSeatDto(seat);
            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateSeat,
                "Seat",
                seat.SeatId.ToString("D"),
                AuditOutcome.Succeeded,
                new { seat.ZoneId, seat.Name, seat.SortOrder },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPatch("/api/branches/{branchId:guid}/layout/seats/{seatId:guid}", async (
            Guid branchId,
            Guid seatId,
            UpdateSeatRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.UpdateSeat,
                    "Seat",
                    seatId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.ZoneId, request.Name, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (request.ZoneId == Guid.Empty)
            {
                return Results.BadRequest(new { Error = "ZoneId is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { Error = "Seat name is required." });
            }

            var zoneExists = await dbContext.Zones.AnyAsync(
                zone =>
                    zone.OrganizationId == request.OrganizationId &&
                    zone.BranchId == branchId &&
                    zone.ZoneId == request.ZoneId,
                cancellationToken);
            if (!zoneExists)
            {
                return Results.NotFound(new { Error = "Zone was not found." });
            }

            var seat = await dbContext.Seats.SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.SeatId == seatId,
                cancellationToken);

            if (seat is null)
            {
                return Results.NotFound(new { Error = "Seat was not found." });
            }

            var trimmedName = request.Name.Trim();
            var normalizedName = trimmedName.ToUpperInvariant();
            var duplicateName = await dbContext.Seats.AnyAsync(
                candidate =>
                    candidate.OrganizationId == request.OrganizationId &&
                    candidate.BranchId == branchId &&
                    candidate.ZoneId == request.ZoneId &&
                    candidate.SeatId != seatId &&
                    candidate.Name.ToUpper() == normalizedName,
                cancellationToken);

            if (duplicateName)
            {
                return Results.Conflict(new { Error = "Seat name already exists in the target zone." });
            }

            seat.ZoneId = request.ZoneId;
            seat.Name = trimmedName;
            seat.SortOrder = request.SortOrder;
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = ToSeatDto(seat);
            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateSeat,
                "Seat",
                seat.SeatId.ToString("D"),
                AuditOutcome.Succeeded,
                new { seat.ZoneId, seat.Name, seat.SortOrder },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapDelete("/api/branches/{branchId:guid}/layout/seats/{seatId:guid}", async (
            Guid branchId,
            Guid seatId,
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ManageLayout,
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
                    AuditActionNames.DeleteSeat,
                    "Seat",
                    seatId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (organizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var seat = await dbContext.Seats.SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.SeatId == seatId,
                cancellationToken);

            if (seat is null)
            {
                return Results.NotFound(new { Error = "Seat was not found." });
            }

            var hasActiveAssignment = await dbContext.DeviceSeatAssignments.AnyAsync(
                assignment =>
                    assignment.OrganizationId == organizationId &&
                    assignment.BranchId == branchId &&
                    assignment.SeatId == seatId &&
                    assignment.DetachedAtUtc == null,
                cancellationToken);

            if (hasActiveAssignment)
            {
                return Results.Conflict(new { Error = "Seat has an active device assignment." });
            }

            var hasSessionHistory = await dbContext.Sessions.AnyAsync(
                session =>
                    session.OrganizationId == organizationId &&
                    session.BranchId == branchId &&
                    session.SeatId == seatId,
                cancellationToken);

            if (hasSessionHistory)
            {
                return Results.Conflict(new { Error = "Seat has session history and cannot be deleted." });
            }

            var zoneId = seat.ZoneId;
            var seatName = seat.Name;
            var sortOrder = seat.SortOrder;
            dbContext.Seats.Remove(seat);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                organizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.DeleteSeat,
                "Seat",
                seatId.ToString("D"),
                AuditOutcome.Succeeded,
                new { ZoneId = zoneId, Name = seatName, SortOrder = sortOrder },
                cancellationToken);

            return Results.NoContent();
        });

    }
}
