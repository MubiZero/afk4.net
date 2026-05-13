using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.FloorMap;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.FloorMap;

public sealed class EfFloorMapReadService(PlatformDbContext dbContext) : IFloorMapReadService
{
    public async Task<FloorMapDto?> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);

        if (branch is null)
        {
            return null;
        }

        var zones = await dbContext.Zones
            .AsNoTracking()
            .Where(zone => zone.BranchId == branchId)
            .ToDictionaryAsync(zone => zone.ZoneId, cancellationToken);
        var seats = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var activeAssignments = await dbContext.DeviceSeatAssignments
            .AsNoTracking()
            .Where(assignment => assignment.BranchId == branchId && assignment.DetachedAtUtc == null)
            .ToListAsync(cancellationToken);
        var assignedDeviceIds = activeAssignments
            .Select(assignment => assignment.DeviceId)
            .ToHashSet();
        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => assignedDeviceIds.Contains(device.DeviceId))
            .ToDictionaryAsync(device => device.DeviceId, cancellationToken);

        var assignmentsBySeat = activeAssignments
            .GroupBy(assignment => assignment.SeatId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.AttachedAtUtc).First());

        var seatStatuses = seats
            .Select(seat => CreateSeatStatus(seat, zones, assignmentsBySeat, devices))
            .OrderBy(seat => zones.TryGetValue(seat.ZoneId, out var zone) ? zone.SortOrder : int.MaxValue)
            .ThenBy(seat => seat.SortOrder)
            .ThenBy(seat => seat.SeatName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FloorMapDto(
            BranchId: branch.BranchId,
            BranchName: branch.Name,
            Seats: seatStatuses);
    }

    private static SeatStatusDto CreateSeatStatus(
        SeatEntity seat,
        IReadOnlyDictionary<Guid, ZoneEntity> zones,
        IReadOnlyDictionary<Guid, DeviceSeatAssignmentEntity> assignmentsBySeat,
        IReadOnlyDictionary<Guid, DeviceEntity> devices)
    {
        zones.TryGetValue(seat.ZoneId, out var zone);

        DeviceEntity? device = null;
        if (assignmentsBySeat.TryGetValue(seat.SeatId, out var assignment))
        {
            devices.TryGetValue(assignment.DeviceId, out device);
        }

        return new SeatStatusDto(
            SeatId: seat.SeatId,
            SeatName: seat.Name,
            ZoneId: seat.ZoneId,
            ZoneName: zone?.Name ?? string.Empty,
            SortOrder: seat.SortOrder,
            State: GetSeatState(device),
            DeviceId: device?.DeviceId,
            DeviceName: device?.MachineName,
            IsDeviceOnline: device?.IsOnline,
            IsDeviceLocked: device?.IsLocked,
            LastHeartbeatAtUtc: device?.LastHeartbeatAtUtc,
            AgentVersion: device?.AgentVersion,
            ShellVersion: device?.ShellVersion,
            ActiveSessionId: null,
            RemainingSeconds: null);
    }

    private static string GetSeatState(DeviceEntity? device)
    {
        if (device is null)
        {
            return "Maintenance";
        }

        if (!device.IsOnline)
        {
            return "Offline";
        }

        return device.IsLocked ? "Locked" : "Free";
    }
}
