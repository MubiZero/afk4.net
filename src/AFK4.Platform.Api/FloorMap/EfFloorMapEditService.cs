using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.FloorMap;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.FloorMap;

public sealed class EfFloorMapEditService(PlatformDbContext dbContext, TimeProvider timeProvider) : IFloorMapEditService
{
    private const int MaxZoneNameLength = 120;
    private const int MaxSeatNameLength = 80;
    private const int MaxClientIdLength = 64;

    public async Task<FloorMapBulkUpdateResult> BulkUpdateAsync(
        Guid organizationId,
        Guid branchId,
        string? ifMatch,
        FloorMapBulkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return new FloorMapBulkUpdateResult(
                FloorMapBulkUpdateStatus.PreconditionRequired,
                "If-Match header is required.",
                null,
                null);
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return new FloorMapBulkUpdateResult(
                FloorMapBulkUpdateStatus.BadRequest,
                validation,
                null,
                null);
        }

        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                cancellationToken);
        if (branch is null)
        {
            return new FloorMapBulkUpdateResult(
                FloorMapBulkUpdateStatus.NotFound,
                "Branch was not found.",
                null,
                null);
        }

        var existingZones = await dbContext.Zones
            .Where(zone => zone.OrganizationId == organizationId && zone.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var existingSeats = await dbContext.Seats
            .Where(seat => seat.OrganizationId == organizationId && seat.BranchId == branchId)
            .ToListAsync(cancellationToken);

        var currentEtag = FloorMapEtag.Compute(existingZones, existingSeats, []);
        if (!EtagMatches(ifMatch, currentEtag))
        {
            return new FloorMapBulkUpdateResult(
                FloorMapBulkUpdateStatus.PreconditionFailed,
                "Floor-map has been modified concurrently. Refresh and retry.",
                null,
                currentEtag);
        }

        if (request.Seats.Any(seat => string.IsNullOrWhiteSpace(seat.ZoneClientId)))
        {
            return new FloorMapBulkUpdateResult(
                FloorMapBulkUpdateStatus.BadRequest,
                "Every seat must reference a zone via ZoneClientId.",
                null,
                null);
        }

        var existingZonesById = existingZones.ToDictionary(zone => zone.ZoneId);
        var existingSeatsById = existingSeats.ToDictionary(seat => seat.SeatId);

        var requestedZoneIds = request.Zones
            .Where(zone => zone.ZoneId.HasValue)
            .Select(zone => zone.ZoneId!.Value)
            .ToHashSet();
        var requestedSeatIds = request.Seats
            .Where(seat => seat.SeatId.HasValue)
            .Select(seat => seat.SeatId!.Value)
            .ToHashSet();

        foreach (var requestedZoneId in requestedZoneIds)
        {
            if (!existingZonesById.ContainsKey(requestedZoneId))
            {
                return new FloorMapBulkUpdateResult(
                    FloorMapBulkUpdateStatus.BadRequest,
                    $"Zone {requestedZoneId:D} does not belong to this branch.",
                    null,
                    null);
            }
        }

        foreach (var requestedSeatId in requestedSeatIds)
        {
            if (!existingSeatsById.ContainsKey(requestedSeatId))
            {
                return new FloorMapBulkUpdateResult(
                    FloorMapBulkUpdateStatus.BadRequest,
                    $"Seat {requestedSeatId:D} does not belong to this branch.",
                    null,
                    null);
            }
        }

        var now = timeProvider.GetUtcNow();
        var zoneAssignments = new List<FloorMapBulkZoneAssignment>(request.Zones.Count);
        var zoneIdByClientId = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var zoneRequest in request.Zones)
        {
            ZoneEntity zone;
            if (zoneRequest.ZoneId is { } zoneId)
            {
                zone = existingZonesById[zoneId];
                zone.Name = zoneRequest.Name.Trim();
                zone.SortOrder = zoneRequest.SortOrder;
            }
            else
            {
                zone = new ZoneEntity
                {
                    ZoneId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    Name = zoneRequest.Name.Trim(),
                    SortOrder = zoneRequest.SortOrder,
                    CreatedAtUtc = now
                };
                dbContext.Zones.Add(zone);
            }

            zoneIdByClientId[zoneRequest.ClientId] = zone.ZoneId;
            zoneAssignments.Add(new FloorMapBulkZoneAssignment(zoneRequest.ClientId, zone.ZoneId));
        }

        var seatAssignments = new List<FloorMapBulkSeatAssignment>(request.Seats.Count);
        var keptSeatIds = new HashSet<Guid>();
        foreach (var seatRequest in request.Seats)
        {
            if (!zoneIdByClientId.TryGetValue(seatRequest.ZoneClientId, out var targetZoneId))
            {
                return new FloorMapBulkUpdateResult(
                    FloorMapBulkUpdateStatus.BadRequest,
                    $"Seat references unknown zone ClientId '{seatRequest.ZoneClientId}'.",
                    null,
                    null);
            }

            SeatEntity seat;
            if (seatRequest.SeatId is { } seatId)
            {
                seat = existingSeatsById[seatId];
                seat.ZoneId = targetZoneId;
                seat.Name = seatRequest.Name.Trim();
                seat.SortOrder = seatRequest.SortOrder;
                keptSeatIds.Add(seatId);
            }
            else
            {
                seat = new SeatEntity
                {
                    SeatId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    ZoneId = targetZoneId,
                    Name = seatRequest.Name.Trim(),
                    SortOrder = seatRequest.SortOrder,
                    CreatedAtUtc = now
                };
                dbContext.Seats.Add(seat);
            }

            seatAssignments.Add(new FloorMapBulkSeatAssignment(seatRequest.ClientId, seat.SeatId));
        }

        var seatsToRemove = existingSeats
            .Where(seat => !keptSeatIds.Contains(seat.SeatId))
            .ToList();
        if (seatsToRemove.Count > 0)
        {
            var seatIds = seatsToRemove.Select(seat => seat.SeatId).ToHashSet();
            var hasAssignments = await dbContext.DeviceSeatAssignments
                .AsNoTracking()
                .AnyAsync(
                    assignment => seatIds.Contains(assignment.SeatId) && assignment.DetachedAtUtc == null,
                    cancellationToken);
            if (hasAssignments)
            {
                return new FloorMapBulkUpdateResult(
                    FloorMapBulkUpdateStatus.Conflict,
                    "Cannot remove seats that still have a device attached.",
                    null,
                    null);
            }

            var hasSessionHistory = await dbContext.Sessions
                .AsNoTracking()
                .AnyAsync(
                    session => seatIds.Contains(session.SeatId),
                    cancellationToken);
            if (hasSessionHistory)
            {
                return new FloorMapBulkUpdateResult(
                    FloorMapBulkUpdateStatus.Conflict,
                    "Cannot remove seats that have session history.",
                    null,
                    null);
            }

            dbContext.Seats.RemoveRange(seatsToRemove);
        }

        var zonesToRemove = existingZones
            .Where(zone => !requestedZoneIds.Contains(zone.ZoneId))
            .ToList();
        if (zonesToRemove.Count > 0)
        {
            dbContext.Zones.RemoveRange(zonesToRemove);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var freshZones = await dbContext.Zones
            .AsNoTracking()
            .Where(zone => zone.OrganizationId == organizationId && zone.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var freshSeats = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.OrganizationId == organizationId && seat.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var freshEtag = FloorMapEtag.Compute(freshZones, freshSeats, []);

        var response = new FloorMapBulkUpdateResponse(freshEtag, zoneAssignments, seatAssignments);
        return new FloorMapBulkUpdateResult(FloorMapBulkUpdateStatus.Success, null, response, freshEtag);
    }

    private static string? ValidateRequest(FloorMapBulkUpdateRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "OrganizationId is required.";
        }

        if (request.Zones.Count == 0)
        {
            return "At least one zone is required.";
        }

        var seenClientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var zone in request.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.ClientId) || zone.ClientId.Length > MaxClientIdLength)
            {
                return "Zone ClientId is required and must be 64 characters or fewer.";
            }

            if (!seenClientIds.Add(zone.ClientId))
            {
                return $"Duplicate zone ClientId '{zone.ClientId}'.";
            }

            if (string.IsNullOrWhiteSpace(zone.Name) || zone.Name.Trim().Length > MaxZoneNameLength)
            {
                return $"Zone name is required and must be {MaxZoneNameLength} characters or fewer.";
            }
        }

        var seenSeatClientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seat in request.Seats)
        {
            if (string.IsNullOrWhiteSpace(seat.ClientId) || seat.ClientId.Length > MaxClientIdLength)
            {
                return "Seat ClientId is required and must be 64 characters or fewer.";
            }

            if (!seenSeatClientIds.Add(seat.ClientId))
            {
                return $"Duplicate seat ClientId '{seat.ClientId}'.";
            }

            if (string.IsNullOrWhiteSpace(seat.Name) || seat.Name.Trim().Length > MaxSeatNameLength)
            {
                return $"Seat name is required and must be {MaxSeatNameLength} characters or fewer.";
            }
        }

        return null;
    }

    private static bool EtagMatches(string ifMatch, string currentEtag)
    {
        var trimmed = ifMatch.Trim();
        if (trimmed == "*")
        {
            return true;
        }

        foreach (var candidate in trimmed.Split(','))
        {
            var value = candidate.Trim();
            if (value.StartsWith("W/", StringComparison.Ordinal))
            {
                value = value[2..];
            }

            if (string.Equals(value, currentEtag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
