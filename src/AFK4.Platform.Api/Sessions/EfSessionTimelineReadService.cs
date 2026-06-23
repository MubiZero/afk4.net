using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

public sealed class EfSessionTimelineReadService(PlatformDbContext dbContext) : ISessionTimelineReadService
{
    public async Task<SessionTimelineResult> GetSessionsAsync(
        Guid organizationId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        // Sessions that actually started and overlap [fromUtc, toUtc]: started on/before the window
        // end and not yet ended OR ended on/after the window start. Requested-but-never-started rows
        // (StartedAtUtc null) carry no timeline span and are skipped.
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.OrganizationId == organizationId &&
                session.BranchId == branchId &&
                session.StartedAtUtc != null &&
                session.StartedAtUtc <= toUtc &&
                (session.EndedAtUtc == null || session.EndedAtUtc >= fromUtc))
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return new SessionTimelineResult([]);
        }

        var seatIds = sessions.Select(session => session.SeatId).ToHashSet();
        var seatsById = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.BranchId == branchId && seatIds.Contains(seat.SeatId))
            .ToDictionaryAsync(seat => seat.SeatId, cancellationToken);

        var zoneIds = seatsById.Values.Select(seat => seat.ZoneId).ToHashSet();
        var zonesById = zoneIds.Count == 0
            ? new Dictionary<Guid, ZoneEntity>()
            : await dbContext.Zones
                .AsNoTracking()
                .Where(zone => zone.BranchId == branchId && zoneIds.Contains(zone.ZoneId))
                .ToDictionaryAsync(zone => zone.ZoneId, cancellationToken);

        var playerAccountIds = sessions
            .Where(session => session.PlayerAccountId is not null)
            .Select(session => session.PlayerAccountId!.Value)
            .ToHashSet();
        var playerAccountsById = playerAccountIds.Count == 0
            ? new Dictionary<Guid, PlayerAccountEntity>()
            : await dbContext.PlayerAccounts
                .AsNoTracking()
                .Where(account => playerAccountIds.Contains(account.PlayerAccountId))
                .ToDictionaryAsync(account => account.PlayerAccountId, cancellationToken);

        // Tariff name resolves session → tariff version → tariff. Guests/packages carry a non-GUID
        // marker instead of a version id and have no named tariff.
        var tariffVersionIds = sessions
            .Select(session => Guid.TryParse(session.TariffRuleVersionId, out var id) ? (Guid?)id : null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();
        var tariffVersionsById = tariffVersionIds.Count == 0
            ? new Dictionary<Guid, TariffVersionEntity>()
            : await dbContext.TariffVersions
                .AsNoTracking()
                .Where(version => version.BranchId == branchId && tariffVersionIds.Contains(version.TariffVersionId))
                .ToDictionaryAsync(version => version.TariffVersionId, cancellationToken);

        var tariffIds = tariffVersionsById.Values.Select(version => version.TariffId).ToHashSet();
        var tariffsById = tariffIds.Count == 0
            ? new Dictionary<Guid, TariffEntity>()
            : await dbContext.Tariffs
                .AsNoTracking()
                .Where(tariff => tariff.BranchId == branchId && tariffIds.Contains(tariff.TariffId))
                .ToDictionaryAsync(tariff => tariff.TariffId, cancellationToken);

        var items = sessions
            .Select(session => MapItem(session, seatsById, zonesById, playerAccountsById, tariffVersionsById, tariffsById))
            .OrderBy(item => item.StartedAtUtc)
            .ToList();

        return new SessionTimelineResult(items);
    }

    private static SessionTimelineItemDto MapItem(
        SessionEntity session,
        IReadOnlyDictionary<Guid, SeatEntity> seatsById,
        IReadOnlyDictionary<Guid, ZoneEntity> zonesById,
        IReadOnlyDictionary<Guid, PlayerAccountEntity> playerAccountsById,
        IReadOnlyDictionary<Guid, TariffVersionEntity> tariffVersionsById,
        IReadOnlyDictionary<Guid, TariffEntity> tariffsById)
    {
        seatsById.TryGetValue(session.SeatId, out var seat);
        ZoneEntity? zone = null;
        if (seat is not null)
        {
            zonesById.TryGetValue(seat.ZoneId, out zone);
        }

        return new SessionTimelineItemDto(
            SessionId: session.SessionId,
            SeatId: session.SeatId,
            SeatName: seat?.Name ?? string.Empty,
            ZoneId: seat?.ZoneId ?? Guid.Empty,
            ZoneName: zone?.Name ?? string.Empty,
            State: session.State,
            PlayerAccountId: session.PlayerAccountId,
            PlayerDisplayName: GetPlayerDisplayName(session, playerAccountsById),
            TariffName: GetTariffName(session, tariffVersionsById, tariffsById),
            // StartedAtUtc is guaranteed non-null by the query filter.
            StartedAtUtc: session.StartedAtUtc!.Value,
            EndsAtUtc: session.EndsAtUtc,
            EndedAtUtc: session.EndedAtUtc);
    }

    private static string? GetPlayerDisplayName(
        SessionEntity session,
        IReadOnlyDictionary<Guid, PlayerAccountEntity> playerAccountsById)
    {
        if (session.PlayerAccountId is not Guid playerAccountId ||
            !playerAccountsById.TryGetValue(playerAccountId, out var account))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(account.DisplayName) ? null : account.DisplayName;
    }

    private static string? GetTariffName(
        SessionEntity session,
        IReadOnlyDictionary<Guid, TariffVersionEntity> tariffVersionsById,
        IReadOnlyDictionary<Guid, TariffEntity> tariffsById)
    {
        if (!Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId) ||
            !tariffVersionsById.TryGetValue(tariffVersionId, out var version) ||
            !tariffsById.TryGetValue(version.TariffId, out var tariff))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(tariff.Name) ? null : tariff.Name;
    }
}
