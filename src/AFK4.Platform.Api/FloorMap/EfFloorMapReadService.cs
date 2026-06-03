using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.FloorMap;

public sealed class EfFloorMapReadService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    BranchDiagnosticsOptions diagnosticsOptions) : IFloorMapReadService
{
    private static readonly string[] ProjectedSessionStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    public EfFloorMapReadService(PlatformDbContext dbContext)
        : this(dbContext, TimeProvider.System, new BranchDiagnosticsOptions())
    {
    }

    public EfFloorMapReadService(PlatformDbContext dbContext, TimeProvider timeProvider)
        : this(dbContext, timeProvider, new BranchDiagnosticsOptions())
    {
    }

    public async Task<FloorMapReadResult?> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
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
            .ToListAsync(cancellationToken);
        var zonesById = zones.ToDictionary(zone => zone.ZoneId);
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
        activeAssignments = activeAssignments
            .Where(assignment =>
                devices.TryGetValue(assignment.DeviceId, out var device) &&
                device.Role == DeviceRoleNames.GamingPc)
            .ToList();
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.BranchId == branchId && ProjectedSessionStates.Contains(session.State))
            .ToListAsync(cancellationToken);

        var assignmentsBySeat = activeAssignments
            .GroupBy(assignment => assignment.SeatId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.AttachedAtUtc).First());
        var sessionsBySeat = sessions
            .GroupBy(session => session.SeatId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(session => session.UpdatedAtUtc).First());

        // Open tabs accrue live cost; load the tariff versions they bill against.
        var openTabTariffVersionIds = sessionsBySeat.Values
            .Where(session => session.EndsAtUtc is null)
            .Select(session => Guid.TryParse(session.TariffRuleVersionId, out var id) ? (Guid?)id : null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();
        var tariffVersionsById = openTabTariffVersionIds.Count == 0
            ? new Dictionary<Guid, TariffVersionEntity>()
            : await dbContext.TariffVersions
                .AsNoTracking()
                .Where(version => version.BranchId == branchId && openTabTariffVersionIds.Contains(version.TariffVersionId))
                .ToDictionaryAsync(version => version.TariffVersionId, cancellationToken);

        var seatStatuses = seats
            .Select(seat => CreateSeatStatus(seat, zonesById, assignmentsBySeat, devices, sessionsBySeat, tariffVersionsById, now))
            .OrderBy(seat => zonesById.TryGetValue(seat.ZoneId, out var zone) ? zone.SortOrder : int.MaxValue)
            .ThenBy(seat => seat.SortOrder)
            .ThenBy(seat => seat.SeatName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var zoneStatuses = zones
            .OrderBy(zone => zone.SortOrder)
            .ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
            .Select(zone => new FloorMapZoneDto(
                ZoneId: zone.ZoneId,
                Name: zone.Name,
                SortOrder: zone.SortOrder))
            .ToList();

        var dto = new FloorMapDto(
            BranchId: branch.BranchId,
            BranchName: branch.Name,
            Seats: seatStatuses)
        {
            Zones = zoneStatuses
        };

        return new FloorMapReadResult(dto, FloorMapEtag.Compute(zones, seats));
    }

    private SeatStatusDto CreateSeatStatus(
        SeatEntity seat,
        IReadOnlyDictionary<Guid, ZoneEntity> zones,
        IReadOnlyDictionary<Guid, DeviceSeatAssignmentEntity> assignmentsBySeat,
        IReadOnlyDictionary<Guid, DeviceEntity> devices,
        IReadOnlyDictionary<Guid, SessionEntity> sessionsBySeat,
        IReadOnlyDictionary<Guid, TariffVersionEntity> tariffVersionsById,
        DateTimeOffset now)
    {
        zones.TryGetValue(seat.ZoneId, out var zone);

        DeviceEntity? device = null;
        if (assignmentsBySeat.TryGetValue(seat.SeatId, out var assignment))
        {
            devices.TryGetValue(assignment.DeviceId, out device);
        }

        sessionsBySeat.TryGetValue(seat.SeatId, out var activeSession);
        var isDeviceOnline = device is null ? (bool?)null : IsHeartbeatFresh(device, now);
        var (accruedCostMinorUnits, currencyCode) = GetAccruedCost(activeSession, tariffVersionsById, now);

        return new SeatStatusDto(
            SeatId: seat.SeatId,
            SeatName: seat.Name,
            ZoneId: seat.ZoneId,
            ZoneName: zone?.Name ?? string.Empty,
            SortOrder: seat.SortOrder,
            State: GetSeatState(device, activeSession, isDeviceOnline),
            DeviceId: device?.DeviceId,
            DeviceName: device?.MachineName,
            IsDeviceOnline: isDeviceOnline,
            IsDeviceLocked: device?.IsLocked,
            LastHeartbeatAtUtc: device?.LastHeartbeatAtUtc,
            AgentVersion: device?.AgentVersion,
            ShellVersion: device?.ShellVersion,
            ActiveSessionId: activeSession?.SessionId,
            RemainingSeconds: GetRemainingSeconds(activeSession, now),
            AccruedCostMinorUnits: accruedCostMinorUnits,
            CurrencyCode: currencyCode);
    }

    private static (long? AccruedCostMinorUnits, string? CurrencyCode) GetAccruedCost(
        SessionEntity? activeSession,
        IReadOnlyDictionary<Guid, TariffVersionEntity> tariffVersionsById,
        DateTimeOffset now)
    {
        // Only open tabs accrue live cost; fixed sessions expose RemainingSeconds.
        if (activeSession?.EndsAtUtc is not null)
        {
            return (null, null);
        }

        if (activeSession is null ||
            !Guid.TryParse(activeSession.TariffRuleVersionId, out var tariffVersionId) ||
            !tariffVersionsById.TryGetValue(tariffVersionId, out var version))
        {
            return (null, null);
        }

        var startedAtUtc = activeSession.StartedAtUtc ?? now;
        var pricing = new TariffPricing(
            version.PricePerMinuteMinorUnits,
            version.MinimumBillableMinutes,
            version.RoundingIncrementMinutes,
            version.CurrencyCode);
        var computation = TariffBilling.ComputeForElapsed(now - startedAtUtc, pricing);
        return computation is null
            ? (null, null)
            : (computation.AmountMinorUnits, computation.CurrencyCode);
    }

    private static int? GetRemainingSeconds(SessionEntity? activeSession, DateTimeOffset now)
    {
        if (activeSession?.EndsAtUtc is null)
        {
            return null;
        }

        return Math.Max(0, (int)(activeSession.EndsAtUtc.Value - now).TotalSeconds);
    }

    private static string GetSeatState(DeviceEntity? device, SessionEntity? activeSession, bool? isDeviceOnline)
    {
        if (activeSession is not null)
        {
            return activeSession.State switch
            {
                SessionStateNames.Ending => "Ending",
                SessionStateNames.Paused => "Paused",
                SessionStateNames.Active => "Active",
                _ => "Active"
            };
        }

        if (device is null)
        {
            return "Maintenance";
        }

        if (device.EnrollmentState != DeviceEnrollmentStateNames.Approved)
        {
            return "Maintenance";
        }

        if (isDeviceOnline != true)
        {
            return "Offline";
        }

        return device.IsLocked ? "Locked" : "Free";
    }

    private bool IsHeartbeatFresh(DeviceEntity device, DateTimeOffset now)
    {
        if (!device.IsOnline || device.LastHeartbeatAtUtc is null)
        {
            return false;
        }

        var staleCutoff = now - TimeSpan.FromSeconds(diagnosticsOptions.StaleHeartbeatSeconds);
        return device.LastHeartbeatAtUtc >= staleCutoff;
    }
}
