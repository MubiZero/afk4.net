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

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    public static async Task<IReadOnlyList<DeviceInventoryItemDto>> LoadBranchDeviceInventoryAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        string? enrollmentState,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Devices
            .AsNoTracking()
            .Where(device => device.OrganizationId == organizationId && device.BranchId == branchId);

        query = enrollmentState is null
            ? query.Where(device => device.EnrollmentState != DeviceEnrollmentStateNames.Removed)
            : query.Where(device => device.EnrollmentState == enrollmentState);

        var devices = await query
            .OrderBy(device => device.MachineName)
            .ThenBy(device => device.DeviceId)
            .ToListAsync(cancellationToken);

        return await BuildDeviceInventoryAsync(dbContext, devices, cancellationToken);
    }

    public static async Task<DeviceInventoryItemDto?> LoadDeviceInventoryItemAsync(
        PlatformDbContext dbContext,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);

        if (device is null)
        {
            return null;
        }

        var items = await BuildDeviceInventoryAsync(dbContext, [device], cancellationToken);
        return items.SingleOrDefault();
    }

    public static async Task<IReadOnlyList<DeviceInventoryItemDto>> BuildDeviceInventoryAsync(
        PlatformDbContext dbContext,
        IReadOnlyList<DeviceEntity> devices,
        CancellationToken cancellationToken)
    {
        if (devices.Count == 0)
        {
            return [];
        }

        var deviceIds = devices.Select(device => device.DeviceId).ToList();
        var assignments = await dbContext.DeviceSeatAssignments
            .AsNoTracking()
            .Where(assignment => deviceIds.Contains(assignment.DeviceId) && assignment.DetachedAtUtc == null)
            .OrderByDescending(assignment => assignment.AttachedAtUtc)
            .ToListAsync(cancellationToken);
        var assignmentsByDevice = assignments
            .GroupBy(assignment => assignment.DeviceId)
            .ToDictionary(group => group.Key, group => group.First());
        var seatIds = assignmentsByDevice.Values
            .Select(assignment => assignment.SeatId)
            .Distinct()
            .ToList();
        var seats = seatIds.Count == 0
            ? []
            : await dbContext.Seats
                .AsNoTracking()
                .Where(seat => seatIds.Contains(seat.SeatId))
                .ToListAsync(cancellationToken);
        var seatsById = seats.ToDictionary(seat => seat.SeatId);
        var zoneIds = seats
            .Select(seat => seat.ZoneId)
            .Distinct()
            .ToList();
        var zones = zoneIds.Count == 0
            ? []
            : await dbContext.Zones
                .AsNoTracking()
                .Where(zone => zoneIds.Contains(zone.ZoneId))
                .ToListAsync(cancellationToken);
        var zonesById = zones.ToDictionary(zone => zone.ZoneId);
        var activeCredentialCounts = await dbContext.DeviceCredentials
            .AsNoTracking()
            .Where(credential => deviceIds.Contains(credential.DeviceId) && credential.RevokedAtUtc == null)
            .GroupBy(credential => credential.DeviceId)
            .Select(group => new { DeviceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);
        var installedAppCounts = await dbContext.DeviceInstalledApps
            .AsNoTracking()
            .Where(app => deviceIds.Contains(app.DeviceId))
            .GroupBy(app => app.DeviceId)
            .Select(group => new { DeviceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);
        var pendingCommandCounts = await dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command => deviceIds.Contains(command.DeviceId) && command.Status == "Pending")
            .GroupBy(command => command.DeviceId)
            .Select(group => new { DeviceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);
        var failedCommandCounts = await dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command => deviceIds.Contains(command.DeviceId) && (command.Status == "Failed" || command.Status == "Rejected"))
            .GroupBy(command => command.DeviceId)
            .Select(group => new { DeviceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.DeviceId, group => group.Count, cancellationToken);

        return devices.Select(device =>
        {
            assignmentsByDevice.TryGetValue(device.DeviceId, out var assignment);
            SeatEntity? seat = null;
            ZoneEntity? zone = null;
            if (assignment is not null && seatsById.TryGetValue(assignment.SeatId, out var assignedSeat))
            {
                seat = assignedSeat;
                zonesById.TryGetValue(assignedSeat.ZoneId, out zone);
            }

            return new DeviceInventoryItemDto(
                OrganizationId: device.OrganizationId,
                BranchId: device.BranchId,
                DeviceId: device.DeviceId,
                MachineName: device.MachineName,
                AgentVersion: device.AgentVersion,
                ShellVersion: device.ShellVersion,
                EnrolledAtUtc: device.EnrolledAtUtc,
                LastHeartbeatAtUtc: device.LastHeartbeatAtUtc,
                IsOnline: device.IsOnline,
                IsLocked: device.IsLocked,
                SeatId: seat?.SeatId,
                SeatName: seat?.Name,
                ZoneId: zone?.ZoneId,
                ZoneName: zone?.Name,
                ActiveCredentialCount: activeCredentialCounts.GetValueOrDefault(device.DeviceId),
                InstalledAppCount: installedAppCounts.GetValueOrDefault(device.DeviceId),
                PendingCommandCount: pendingCommandCounts.GetValueOrDefault(device.DeviceId),
                FailedCommandCount: failedCommandCounts.GetValueOrDefault(device.DeviceId),
                DisplayName: string.IsNullOrWhiteSpace(device.DisplayName) ? device.MachineName : device.DisplayName,
                Role: device.Role,
                EnrollmentState: device.EnrollmentState);
        }).ToList();
    }

    public static async Task<DeviceMutationScope> LoadDeviceMutationScopeAsync(
        PlatformDbContext dbContext,
        IStaffContextAccessor staffContextAccessor,
        StaffAuthorizationService authorizationService,
        IAuditRecordWriter auditRecordWriter,
        Guid deviceId,
        string permission,
        string auditAction,
        object details,
        CancellationToken cancellationToken)
    {
        var staffContext = staffContextAccessor.Current;
        if (staffContext is null)
        {
            return new DeviceMutationScope(null, null, Results.Unauthorized());
        }

        var device = await dbContext.Devices
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.DeviceId == deviceId &&
                    candidate.OrganizationId == staffContext.OrganizationId,
                cancellationToken);

        if (device is null)
        {
            return new DeviceMutationScope(null, null, Results.NotFound());
        }

        var authorization = await authorizationService.RequireBranchPermissionAsync(
            device.BranchId,
            permission,
            cancellationToken);

        if (!authorization.IsAuthenticated)
        {
            return new DeviceMutationScope(device, authorization, Results.Unauthorized());
        }

        if (!authorization.IsAllowed)
        {
            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext!.OrganizationId,
                device.BranchId,
                authorization.StaffContext.StaffUserId,
                auditAction,
                "Device",
                device.DeviceId.ToString("D"),
                AuditOutcome.Denied,
                new
                {
                    authorization.DenialReason,
                    Details = details
                },
                cancellationToken);

            return new DeviceMutationScope(device, authorization, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        return new DeviceMutationScope(device, authorization, null);
    }

    public static IResult? ValidateDeviceMutationOrganization(
        Guid requestOrganizationId,
        StaffAuthorizationResult authorization,
        DeviceEntity device)
    {
        if (requestOrganizationId == Guid.Empty)
        {
            return Results.BadRequest(new { Error = "OrganizationId is required." });
        }

        if (requestOrganizationId != authorization.StaffContext!.OrganizationId ||
            requestOrganizationId != device.OrganizationId)
        {
            return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization and device." });
        }

        return null;
    }

    public static async Task<DeviceSeatAssignmentOperationResult> ApplyDeviceSeatAssignmentAsync(
        PlatformDbContext dbContext,
        DeviceEntity device,
        Guid organizationId,
        Guid seatId,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var seat = await dbContext.Seats
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.SeatId == seatId &&
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == device.BranchId,
                cancellationToken);

        if (seat is null)
        {
            return new DeviceSeatAssignmentOperationResult(null, Results.NotFound(), [device.DeviceId], observedAtUtc);
        }

        var hasActiveSession = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == device.BranchId &&
                    (candidate.SeatId == seatId || candidate.DeviceId == device.DeviceId) &&
                    (candidate.State == SessionStateNames.Active ||
                     candidate.State == SessionStateNames.Paused ||
                     candidate.State == SessionStateNames.Ending),
                cancellationToken);

        if (hasActiveSession)
        {
            return new DeviceSeatAssignmentOperationResult(
                null,
                Results.Conflict(new { Error = "Seat or device has an active, paused, or ending session." }),
                [device.DeviceId],
                observedAtUtc);
        }

        var activeAssignments = await dbContext.DeviceSeatAssignments
            .Where(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == device.BranchId &&
                    candidate.DetachedAtUtc == null &&
                    (candidate.SeatId == seatId || candidate.DeviceId == device.DeviceId))
            .OrderByDescending(candidate => candidate.AttachedAtUtc)
            .ThenByDescending(candidate => candidate.DeviceSeatAssignmentId)
            .ToListAsync(cancellationToken);

        var changedDeviceIds = activeAssignments
            .Select(candidate => candidate.DeviceId)
            .Append(device.DeviceId)
            .Distinct()
            .ToArray();
        var currentAssignment = activeAssignments.FirstOrDefault(
            candidate => candidate.SeatId == seatId && candidate.DeviceId == device.DeviceId);

        if (currentAssignment is not null)
        {
            foreach (var assignment in activeAssignments.Where(candidate => candidate.DeviceSeatAssignmentId != currentAssignment.DeviceSeatAssignmentId))
            {
                assignment.DetachedAtUtc = observedAtUtc;
            }
        }
        else
        {
            foreach (var assignment in activeAssignments)
            {
                assignment.DetachedAtUtc = observedAtUtc;
            }

            currentAssignment = new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = device.BranchId,
                SeatId = seatId,
                DeviceId = device.DeviceId,
                AttachedAtUtc = observedAtUtc
            };
            dbContext.DeviceSeatAssignments.Add(currentAssignment);
        }

        return new DeviceSeatAssignmentOperationResult(currentAssignment, null, changedDeviceIds, observedAtUtc);
    }

    public static async Task<IReadOnlyList<Guid>> DetachActiveDeviceAssignmentsAsync(
        PlatformDbContext dbContext,
        DeviceEntity device,
        DateTimeOffset detachedAtUtc,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.DeviceSeatAssignments
            .Where(
                assignment =>
                    assignment.OrganizationId == device.OrganizationId &&
                    assignment.BranchId == device.BranchId &&
                    assignment.DeviceId == device.DeviceId &&
                    assignment.DetachedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            assignment.DetachedAtUtc = detachedAtUtc;
        }

        return assignments.Count == 0
            ? [device.DeviceId]
            : assignments
                .Select(assignment => assignment.DeviceId)
                .Append(device.DeviceId)
                .Distinct()
                .ToArray();
    }

    public static async Task<int> RevokeActiveDeviceCredentialsAsync(
        PlatformDbContext dbContext,
        DeviceEntity device,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        var credentials = await dbContext.DeviceCredentials
            .Where(
                credential =>
                    credential.OrganizationId == device.OrganizationId &&
                    credential.BranchId == device.BranchId &&
                    credential.DeviceId == device.DeviceId &&
                    credential.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var credential in credentials)
        {
            credential.RevokedAtUtc = revokedAtUtc;
        }

        return credentials.Count;
    }

    public static async Task<bool> HasActiveDeviceSessionAsync(
        PlatformDbContext dbContext,
        DeviceEntity device,
        CancellationToken cancellationToken)
    {
        return await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(
                session =>
                    session.OrganizationId == device.OrganizationId &&
                    session.BranchId == device.BranchId &&
                    session.DeviceId == device.DeviceId &&
                    (session.State == SessionStateNames.Active ||
                     session.State == SessionStateNames.Paused ||
                     session.State == SessionStateNames.Ending),
                cancellationToken);
    }

    public static async Task NotifyDeviceChangesAsync(
        IHubContext<DeviceHub> hubContext,
        PlatformDbContext dbContext,
        IEnumerable<Guid> deviceIds,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var ids = deviceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => ids.Contains(device.DeviceId))
            .ToListAsync(cancellationToken);
        var assignmentRows = await dbContext.DeviceSeatAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.DeviceId) && assignment.DetachedAtUtc == null)
            .OrderByDescending(assignment => assignment.AttachedAtUtc)
            .ThenByDescending(assignment => assignment.DeviceSeatAssignmentId)
            .ToListAsync(cancellationToken);
        var assignments = assignmentRows
            .GroupBy(assignment => assignment.DeviceId)
            .ToDictionary(group => group.Key, group => group.First().SeatId);

        foreach (var device in devices)
        {
            var seatId = assignments.TryGetValue(device.DeviceId, out var assignedSeatId)
                ? assignedSeatId
                : (Guid?)null;
            var status = new DeviceStatusChangedDto(
                OrganizationId: device.OrganizationId,
                BranchId: device.BranchId,
                DeviceId: device.DeviceId,
                MachineName: device.MachineName,
                IsOnline: device.IsOnline,
                IsLocked: device.IsLocked,
                ObservedAtUtc: observedAtUtc,
                DisplayName: string.IsNullOrWhiteSpace(device.DisplayName) ? device.MachineName : device.DisplayName,
                Role: device.Role,
                EnrollmentState: device.EnrollmentState,
                SeatId: seatId);

            await hubContext.Clients
                .Group(DeviceHubGroups.Branch(device.BranchId))
                .SendAsync(DeviceRealtimeEvents.DeviceStatusChanged, status, cancellationToken);
        }
    }

    public static async Task<IResult> CompleteReconciliationAsync(
        PlatformDbContext dbContext,
        IDeviceCommandDispatchService commandDispatchService,
        DeviceSessionSnapshotRequest request,
        string action,
        string reason,
        SessionEntity? session,
        SessionLeaseDto? lease,
        bool dispatchCommand,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        var sessionId = session?.SessionId ?? request.ActiveSessionId ?? request.ActiveLease?.SessionId;

        var shouldDispatchCommand = dispatchCommand &&
            (action != "lock" ||
                sessionId is null ||
                !await HasInFlightOrAcceptedLockCommandAsync(
                    dbContext,
                    request.DeviceId,
                    sessionId.Value,
                    cancellationToken));

        if (shouldDispatchCommand)
        {
            var payload = CreateReconciliationCommandPayload(action, reason, sessionId, lease);
            await commandDispatchService.DispatchAsync(
                request.DeviceId,
                new CreateDeviceCommandRequest(action, payload),
                cancellationToken);
        }

        if (session is not null)
        {
            dbContext.SessionEvents.Add(new SessionEventEntity
            {
                SessionEventId = Guid.NewGuid(),
                SessionId = session.SessionId,
                OrganizationId = session.OrganizationId,
                BranchId = session.BranchId,
                EventType = "device-reconciled",
                ActorStaffUserId = null,
                DeviceId = request.DeviceId,
                CreatedAtUtc = recordedAtUtc,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    action,
                    reason,
                    request.ActiveSessionId,
                    request.ObservedAtUtc,
                    request.PendingLocalEventCount
                })
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(new SessionReconciliationResponse(
            Action: action,
            Reason: reason,
            SessionId: sessionId,
            Lease: lease));
    }

    public static async Task<bool> HasInFlightOrAcceptedLockCommandAsync(
        PlatformDbContext dbContext,
        Guid deviceId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var commands = await dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command =>
                command.DeviceId == deviceId &&
                command.Type == DeviceCommandTypeNames.Lock &&
                (command.Status == "Pending" ||
                    command.Status == "Accepted" ||
                    command.Status == "Completed"))
            .Select(command => command.PayloadJson)
            .ToListAsync(cancellationToken);

        return commands.Any(payloadJson => TryReadCommandSessionId(payloadJson) == sessionId);
    }

    public static Guid? TryReadCommandSessionId(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty("sessionId", out var sessionIdElement) &&
                sessionIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(sessionIdElement.GetString(), out var sessionId)
                    ? sessionId
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Dictionary<string, string> CreateReconciliationCommandPayload(
        string action,
        string reason,
        Guid? sessionId,
        SessionLeaseDto? lease)
    {
        var payload = new Dictionary<string, string>
        {
            ["reason"] = reason
        };

        if (sessionId is not null)
        {
            payload["sessionId"] = sessionId.Value.ToString("D");
        }

        if (action == "unlock" && lease is not null)
        {
            payload["sessionLease"] = JsonSerializer.Serialize(lease, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        return payload;
    }

    public static bool LocalLeaseMatches(
        DeviceSessionSnapshotRequest request,
        SessionEntity cloudSession,
        SessionLeaseDto currentLease,
        DateTimeOffset now)
    {
        var localLease = request.ActiveLease;

        return localLease is not null &&
            request.ActiveSessionId == cloudSession.SessionId &&
            localLease.SessionId == cloudSession.SessionId &&
            localLease.OrganizationId == cloudSession.OrganizationId &&
            localLease.BranchId == cloudSession.BranchId &&
            localLease.DeviceId == cloudSession.DeviceId &&
            localLease.SeatId == cloudSession.SeatId &&
            localLease.Sequence == currentLease.Sequence &&
            string.Equals(localLease.Signature, currentLease.Signature, StringComparison.Ordinal) &&
            localLease.ExpiresAtUtc > now;
    }

    public static async Task<SessionLeaseDto?> LoadCurrentLeaseAsync(
        PlatformDbContext dbContext,
        SessionEntity session,
        CancellationToken cancellationToken)
    {
        var leaseEntity = session.CurrentLeaseId is null
            ? await dbContext.SessionLeases
                .Where(lease => lease.SessionId == session.SessionId)
                .OrderByDescending(lease => lease.Sequence)
                .FirstOrDefaultAsync(cancellationToken)
            : await dbContext.SessionLeases
                .SingleOrDefaultAsync(lease => lease.SessionLeaseId == session.CurrentLeaseId, cancellationToken);

        if (leaseEntity is null)
        {
            return null;
        }

        var lease = JsonSerializer.Deserialize<SessionLeaseDto>(
            leaseEntity.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return lease ?? new SessionLeaseDto(
            SessionId: leaseEntity.SessionId,
            OrganizationId: leaseEntity.OrganizationId,
            BranchId: leaseEntity.BranchId,
            SeatId: leaseEntity.SeatId,
            DeviceId: leaseEntity.DeviceId,
            State: leaseEntity.State,
            Sequence: leaseEntity.Sequence,
            IssuedAtUtc: leaseEntity.IssuedAtUtc,
            ExpiresAtUtc: leaseEntity.ExpiresAtUtc,
            SignatureAlgorithm: leaseEntity.SignatureAlgorithm,
            Signature: leaseEntity.Signature);
    }
}
