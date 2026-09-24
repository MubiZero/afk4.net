using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHeartbeatService(
    IHubContext<DeviceHub> hubContext,
    PlatformDbContext dbContext,
    IHeartbeatSessionCommandPlanner sessionCommandPlanner,
    IDeviceCommandDispatchService commandDispatchService,
    IOptions<SessionLeaseOptions> leaseOptions,
    IOptions<HeartbeatOptions> heartbeatOptions,
    EfSeatingCodeService seatingCodes,
    IDeviceBoundPlayerTokens deviceTokens,
    IOrganizationFeatureSnapshot featureSnapshot,
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

        // Место и его зона — тем же запросом, что и само место: оболочке нужно «ПК 07 · Общий зал»,
        // и отдельный запрос за именами на каждое сердцебиение стоил бы столько же, сколько место.
        SeatOfDevice? seat = null;
        if (device is not null)
        {
            seat = await dbContext.DeviceSeatAssignments
                .AsNoTracking()
                .Where(assignment => assignment.DeviceId == deviceId && assignment.DetachedAtUtc == null)
                .OrderByDescending(assignment => assignment.AttachedAtUtc)
                .ThenByDescending(assignment => assignment.DeviceSeatAssignmentId)
                .Select(assignment => new SeatOfDevice(
                    assignment.SeatId,
                    dbContext.Seats
                        .Where(candidate => candidate.SeatId == assignment.SeatId)
                        .Select(candidate => candidate.Name)
                        .FirstOrDefault(),
                    dbContext.Seats
                        .Where(candidate => candidate.SeatId == assignment.SeatId)
                        .SelectMany(candidate => dbContext.Zones
                            .Where(zone => zone.ZoneId == candidate.ZoneId)
                            .Select(zone => zone.Name))
                        .FirstOrDefault()))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var seatId = seat?.SeatId;

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

        // Оформление берётся тем же запросом, что и окно офлайна: сердцебиение стучит с каждой
        // машины раз в десять секунд, и отдельный запрос за логотипом стоил бы ровно столько же,
        // сколько сам логотип меняется раз в полгода.
        var branchInfo = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.BranchId == request.BranchId)
            .Select(branch => new
            {
                branch.GraceLeaseMinutes,
                Branding = dbContext.Organizations
                    .Where(organization => organization.OrganizationId == branch.OrganizationId)
                    .Select(organization => new ShellBrandingDto(
                        organization.Name, organization.LogoUrl, organization.AccentColor))
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var branchGraceMinutes = branchInfo?.GraceLeaseMinutes ?? 0;

        // Код показывает только свободная машина: звать человека к занятой незачем, а показать
        // код поверх чужой игры значит позвать к ней постороннего.
        //
        // Тот же запрос говорит и чья сессия: вошедшему не владельцу оболочка её не откроет.
        var liveSession = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.DeviceId == request.DeviceId
                && (session.State == SessionStateNames.Active
                    || session.State == SessionStateNames.Paused
                    || session.State == SessionStateNames.Ending))
            .Select(session => new { session.PlayerAccountId })
            .FirstOrDefaultAsync(cancellationToken);
        var busy = liveSession is not null;

        var seatingCode = busy || !allowOperationalCommands
            ? null
            : await seatingCodes.IssueAsync(request.OrganizationId, request.DeviceId, cancellationToken);

        // Свободная машина: вход, от которого не осталось сессии, гаснет здесь, а не по доброй
        // воле хоста (спека оболочки, §5.3).
        if (!busy && device is not null)
        {
            await deviceTokens.ExpireIdleAsync(deviceId, cancellationToken);
        }

        var features = allowOperationalCommands
            ? await featureSnapshot.GetEnabledAsync(request.OrganizationId, cancellationToken)
            : null;

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
            RotateCredential: allowOperationalCommands && rotationRequested,
            Branding: branchInfo?.Branding,
            Seat: seat is null ? null : new DeviceSeatDto(seat.Label ?? string.Empty, seat.ZoneName),
            SessionOwner: liveSession switch
            {
                null => new DeviceSessionOwnerDto(DeviceSessionOwnerKindNames.None),
                { PlayerAccountId: { } playerAccountId } => new DeviceSessionOwnerDto(
                    DeviceSessionOwnerKindNames.Player, playerAccountId),
                _ => new DeviceSessionOwnerDto(DeviceSessionOwnerKindNames.Guest)
            },
            Features: features);
    }

    private sealed record SeatOfDevice(Guid SeatId, string? Label, string? ZoneName);
}
