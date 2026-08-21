using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Карточка заявки — одна на всё приложение: и для списка на стойке, и для той же заявки после
/// посадки гостя.
///
/// Две проекции одного DTO расходятся молча: у одной поле заполнено, у другой всегда пусто, и
/// стойка теряет цену ровно в тот момент, когда сажает человека. Поэтому места, где заявка
/// превращается в карточку, здесь ровно одно.
/// </summary>
internal static class ReservationProjection
{
    public static async Task<IReadOnlyList<ReservationDto>> ProjectAsync(
        PlatformDbContext dbContext,
        IReadOnlyList<ReservationEntity> reservations,
        CancellationToken cancellationToken)
    {
        var seatIds = reservations
            .Where(reservation => reservation.SeatId is not null)
            .Select(reservation => reservation.SeatId!.Value)
            .Distinct()
            .ToList();
        var seats = seatIds.Count == 0
            ? new List<SeatEntity>()
            : await dbContext.Seats
                .AsNoTracking()
                .Where(seat => seatIds.Contains(seat.SeatId))
                .ToListAsync(cancellationToken);
        var zoneIds = seats.Select(seat => seat.ZoneId).Distinct().ToList();
        var zones = zoneIds.Count == 0
            ? new List<ZoneEntity>()
            : await dbContext.Zones
                .AsNoTracking()
                .Where(zone => zoneIds.Contains(zone.ZoneId))
                .ToListAsync(cancellationToken);
        var seatById = seats.ToDictionary(seat => seat.SeatId);
        var zoneById = zones.ToDictionary(zone => zone.ZoneId);

        // Личность за счётом — одним запросом на всю выборку: в списке дня заявок десятки, и
        // спрашивать по строке значит платить за экран сотней обращений к базе.
        var playerAccountIds = reservations
            .Where(reservation => reservation.PlayerAccountId is not null)
            .Select(reservation => reservation.PlayerAccountId!.Value)
            .Distinct()
            .ToList();
        var personByAccountId = playerAccountIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await dbContext.PlayerAccounts
                .AsNoTracking()
                .Where(account => playerAccountIds.Contains(account.PlayerAccountId)
                    && account.PlatformPersonId != null)
                .Select(account => new { account.PlayerAccountId, PersonId = account.PlatformPersonId!.Value })
                .ToDictionaryAsync(row => row.PlayerAccountId, row => row.PersonId, cancellationToken);

        // Имя тарифа — для показа, а не для расчёта: сумма уже посчитана и записана при брони.
        // Версия могла быть снята с публикации, поэтому имя берётся по самой версии, а не по
        // действующему на сегодня прайсу.
        var tariffVersionIds = reservations
            .Where(reservation => reservation.TariffVersionId is not null)
            .Select(reservation => reservation.TariffVersionId!.Value)
            .Distinct()
            .ToList();
        var tariffNameByVersionId = tariffVersionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.TariffVersions
                .AsNoTracking()
                .Where(version => tariffVersionIds.Contains(version.TariffVersionId))
                .Join(
                    dbContext.Tariffs.AsNoTracking(),
                    version => version.TariffId,
                    tariff => tariff.TariffId,
                    (version, tariff) => new { version.TariffVersionId, tariff.Name })
                .ToDictionaryAsync(row => row.TariffVersionId, row => row.Name, cancellationToken);

        return reservations.Select(reservation =>
        {
            SeatEntity? seat = null;
            if (reservation.SeatId is not null)
            {
                seatById.TryGetValue(reservation.SeatId.Value, out seat);
            }

            var zoneName = seat is not null && zoneById.TryGetValue(seat.ZoneId, out var zone)
                ? zone.Name
                : null;

            return new ReservationDto(
                reservation.ReservationId,
                reservation.OrganizationId,
                reservation.BranchId,
                reservation.PlayerAccountId,
                reservation.SeatId,
                seat?.Name,
                zoneName,
                reservation.CustomerName,
                reservation.PhoneNumber,
                reservation.StartsAtUtc,
                reservation.EndsAtUtc,
                DurationMinutes(reservation),
                reservation.State,
                reservation.Source,
                reservation.Note,
                reservation.CreatedAtUtc,
                reservation.UpdatedAtUtc,
                reservation.CancelledAtUtc,
                reservation.CancelReason,
                reservation.ReservationGroupId,
                reservation.Version,
                reservation.StartedSessionId,
                reservation.TariffVersionId,
                reservation.TariffVersionId is { } tariffVersionId &&
                    tariffNameByVersionId.TryGetValue(tariffVersionId, out var tariffName)
                        ? tariffName
                        : null,
                reservation.EstimatedCostMinorUnits,
                reservation.CurrencyCode,
                reservation.RespondByUtc,
                reservation.ConfirmedAtUtc,
                reservation.PlayerAccountId is { } playerAccountId &&
                    personByAccountId.TryGetValue(playerAccountId, out var platformPersonId)
                        ? platformPersonId
                        : null,
                reservation.NoShowAtUtc,
                reservation.RetainedAmountMinorUnits,
                reservation.RejectedAtUtc,
                reservation.RejectReasonCode,
                reservation.RejectReasonNote);
        }).ToList();
    }

    /// <summary>Одна заявка тем же расчётом, что и целый список.</summary>
    public static async Task<ReservationDto> ProjectOneAsync(
        PlatformDbContext dbContext,
        ReservationEntity reservation,
        CancellationToken cancellationToken) =>
        (await ProjectAsync(dbContext, [reservation], cancellationToken))[0];

    private static int DurationMinutes(ReservationEntity reservation) =>
        Math.Max(1, (int)Math.Round((reservation.EndsAtUtc - reservation.StartsAtUtc).TotalMinutes));
}
