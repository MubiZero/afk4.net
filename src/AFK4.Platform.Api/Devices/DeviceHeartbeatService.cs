using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHeartbeatService(
    IHubContext<DeviceHub> hubContext,
    PlatformDbContext dbContext,
    IHeartbeatSessionCommandPlanner sessionCommandPlanner,
    IDeviceCommandDispatchService commandDispatchService,
    IOptions<SessionLeaseOptions> leaseOptions,
    IOptions<HeartbeatOptions> heartbeatOptions,
    EfSeatingCodeService seatingCodes,
    TimeProvider timeProvider) : IDeviceHeartbeatService
{
    public async Task<DeviceHeartbeatResponse> RecordHeartbeatAsync(
        Guid deviceId,
        DeviceHeartbeatRequest request,
        bool allowOperationalCommands,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate =>
                candidate.DeviceId == deviceId &&
                candidate.OrganizationId == request.OrganizationId &&
                candidate.BranchId == request.BranchId,
            cancellationToken);

        if (device is not null)
        {
            device.MachineName = request.MachineName;
            device.AgentVersion = request.AgentVersion;
            device.ShellVersion = request.ShellVersion;
            device.LastHeartbeatAtUtc = request.ObservedAtUtc;
            device.IsOnline = true;
            device.IsLocked = request.IsLocked;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        Guid? seatId = null;
        if (device is not null)
        {
            seatId = await dbContext.DeviceSeatAssignments
                .AsNoTracking()
                .Where(assignment => assignment.DeviceId == deviceId && assignment.DetachedAtUtc == null)
                .OrderByDescending(assignment => assignment.AttachedAtUtc)
                .ThenByDescending(assignment => assignment.DeviceSeatAssignmentId)
                .Select(assignment => (Guid?)assignment.SeatId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var status = new DeviceStatusChangedDto(
            OrganizationId: request.OrganizationId,
            BranchId: request.BranchId,
            DeviceId: deviceId,
            MachineName: request.MachineName,
            IsOnline: true,
            IsLocked: request.IsLocked,
            ObservedAtUtc: request.ObservedAtUtc,
            DisplayName: device is null || string.IsNullOrWhiteSpace(device.DisplayName) ? request.MachineName : device.DisplayName,
            Role: device?.Role ?? string.Empty,
            EnrollmentState: device?.EnrollmentState ?? string.Empty,
            SeatId: seatId);

        if (allowOperationalCommands)
        {
            await hubContext.Clients
                .Group(DeviceHubGroups.Branch(request.BranchId))
                .SendAsync(DeviceRealtimeEvents.DeviceStatusChanged, status, cancellationToken);
        }

        if (device is not null && allowOperationalCommands)
        {
            var plannedCommands = await sessionCommandPlanner.PlanAsync(deviceId, request, cancellationToken);
            foreach (var plan in plannedCommands)
            {
                await commandDispatchService.EnqueueAsync(plan.DeviceId, plan.Command, cancellationToken);
            }
        }

        var commands = new List<DeviceCommandDto>();
        if (allowOperationalCommands)
        {
            var pendingCommands = await dbContext.DeviceCommands
                .AsNoTracking()
                .Where(command => command.DeviceId == deviceId && command.Status == "Pending")
                .OrderBy(command => command.CreatedAtUtc)
                .ThenBy(command => command.CommandId)
                .Select(command => new
                {
                    command.CommandId,
                    command.Type,
                    command.CreatedAtUtc,
                    command.PayloadJson
                })
                .ToListAsync(cancellationToken);

            commands = pendingCommands
                .Select(command => new DeviceCommandDto(
                    command.CommandId,
                    command.Type,
                    command.CreatedAtUtc,
                    JsonSerializer.Deserialize<Dictionary<string, string>>(command.PayloadJson) ?? []))
                .ToList();
        }

        var branchGraceMinutes = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.BranchId == request.BranchId)
            .Select(branch => branch.GraceLeaseMinutes)
            .FirstOrDefaultAsync(cancellationToken);

        // Код показывает только свободная машина: звать человека к занятой незачем, а показать
        // код поверх чужой игры значит позвать к ней постороннего.
        var busy = await dbContext.Sessions
            .AsNoTracking()
            .AnyAsync(
                session => session.DeviceId == request.DeviceId
                    && (session.State == SessionStateNames.Active
                        || session.State == SessionStateNames.Paused
                        || session.State == SessionStateNames.Ending),
                cancellationToken);

        var seatingCode = busy || !allowOperationalCommands
            ? null
            : await seatingCodes.IssueAsync(request.OrganizationId, request.DeviceId, cancellationToken);

        var rotationRequested = device?.CredentialRotationRequestedAtUtc is not null;

        return new DeviceHeartbeatResponse(
            ServerTimeUtc: timeProvider.GetUtcNow(),
            HeartbeatIntervalSeconds: HeartbeatIntervalPolicy.Resolve(commands.Count > 0, heartbeatOptions.Value),
            Commands: commands,
            EffectiveGraceMinutes: GraceLeasePolicy.Resolve(branchGraceMinutes, leaseOptions.Value.LeaseMinutes),
            SeatingCode: seatingCode?.Code,
            SeatingCodeExpiresAtUtc: seatingCode?.ExpiresAtUtc,
            // Просьбу видит только машина, которую клуб уже принял: незаверенному ПК менять
            // нечего, а просьба на нём выглядела бы как разрешение.
            RotateCredential: allowOperationalCommands && rotationRequested);
    }
}
