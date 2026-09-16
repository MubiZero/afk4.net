using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Operator;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Search;

/// <summary>
/// Поиск по филиалу для палитры оператора: места, клиенты, брони и чеки одной строкой.
///
/// Каждый вид ищется по тому, чем его называют вслух: место — по имени ПК, клиент — по имени и
/// телефону, бронь — по имени и телефону гостя, чек — по номеру. Идентификаторов в поиске нет
/// намеренно: guid в палитре никто не набирает.
/// </summary>
public sealed class EfBranchSearchService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IBranchSearchService
{
    // Одна буква совпала бы с половиной базы и гоняла бы запрос впустую на каждом нажатии.
    private const int MinimumQueryLength = 2;

    private const int DefaultPerKindLimit = 5;

    private const int MaximumPerKindLimit = 10;

    public async Task<IReadOnlyList<BranchSearchResultDto>> SearchAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        BranchSearchScope scope,
        int perKindLimit,
        CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length < MinimumQueryLength || scope.Nothing)
        {
            return [];
        }

        var take = perKindLimit <= 0
            ? DefaultPerKindLimit
            : Math.Min(perKindLimit, MaximumPerKindLimit);
        var normalized = trimmed.ToUpperInvariant();
        // Номер набирают подряд цифрами, а хранится он с пробелами и дефисами: сравниваем цифры.
        // Пусто — значит искали не номер, и телефонную ветку в запросе просто не включаем.
        var digits = PhoneQuery.Digits(trimmed);

        var results = new List<BranchSearchResultDto>();
        if (scope.Seats)
        {
            results.AddRange(await SearchSeatsAsync(organizationId, branchId, normalized, take, cancellationToken));
        }

        if (scope.Players)
        {
            results.AddRange(await SearchPlayersAsync(organizationId, branchId, normalized, digits, take, cancellationToken));
        }

        if (scope.Reservations)
        {
            results.AddRange(await SearchReservationsAsync(organizationId, branchId, normalized, digits, take, cancellationToken));
        }

        if (scope.Receipts)
        {
            results.AddRange(await SearchReceiptsAsync(organizationId, branchId, normalized, take, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Место ищется и по имени ПК на стене, и по имени машины в сети: оператор помнит первое,
    /// а в заявке из поддержки приходит второе. Одним запросом с приклеенными залом и машиной —
    /// в зале на двести мест вытаскивать весь список на каждое нажатие было бы расточительно.
    /// </summary>
    private async Task<IReadOnlyList<BranchSearchResultDto>> SearchSeatsAsync(
        Guid organizationId,
        Guid branchId,
        string normalized,
        int take,
        CancellationToken cancellationToken)
    {
        var found = await (
            from seat in dbContext.Seats.AsNoTracking()
            join zoneRow in dbContext.Zones.AsNoTracking() on seat.ZoneId equals zoneRow.ZoneId into zoneMatches
            from zone in zoneMatches.DefaultIfEmpty()
            join assignmentRow in dbContext.DeviceSeatAssignments.AsNoTracking()
                    .Where(assignment => assignment.DetachedAtUtc == null)
                on seat.SeatId equals assignmentRow.SeatId into assignmentMatches
            from assignment in assignmentMatches.DefaultIfEmpty()
            join deviceRow in dbContext.Devices.AsNoTracking() on assignment.DeviceId equals deviceRow.DeviceId into deviceMatches
            from device in deviceMatches.DefaultIfEmpty()
            where seat.OrganizationId == organizationId
                && seat.BranchId == branchId
                && (seat.Name.ToUpper().Contains(normalized)
                    || (device != null && (device.DisplayName.ToUpper().Contains(normalized)
                        || device.MachineName.ToUpper().Contains(normalized))))
            orderby seat.SortOrder, seat.Name
            select new
            {
                seat.SeatId,
                seat.Name,
                ZoneName = zone != null ? zone.Name : null,
                DeviceName = device != null ? device.DisplayName : null
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        return found
            .Select(seat =>
            {
                // Зал и имя машины — то, чем различают два похожих места; нет ни того ни другого —
                // подписи не выдумываем.
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(seat.ZoneName))
                {
                    parts.Add(seat.ZoneName);
                }

                if (!string.IsNullOrWhiteSpace(seat.DeviceName) &&
                    !string.Equals(seat.DeviceName, seat.Name, StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(seat.DeviceName);
                }

                return new BranchSearchResultDto(
                    BranchSearchKindNames.Seat,
                    seat.SeatId,
                    seat.Name,
                    parts.Count == 0 ? null : string.Join(" · ", parts));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<BranchSearchResultDto>> SearchPlayersAsync(
        Guid organizationId,
        Guid branchId,
        string normalized,
        string digits,
        int take,
        CancellationToken cancellationToken)
    {
        var players = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(player =>
                player.OrganizationId == organizationId &&
                player.HomeBranchId == branchId &&
                player.IsActive &&
                (player.DisplayName.ToUpper().Contains(normalized) ||
                    (digits.Length > 0 && player.PhoneNumber != null &&
                        player.PhoneNumber
                            .Replace(" ", string.Empty)
                            .Replace("-", string.Empty)
                            .Replace("(", string.Empty)
                            .Replace(")", string.Empty)
                            .Replace("+", string.Empty)
                            .Contains(digits))))
            .OrderBy(player => player.DisplayName)
            .ThenBy(player => player.PlayerAccountId)
            .Take(take)
            .Select(player => new BranchSearchResultDto(
                BranchSearchKindNames.Player,
                player.PlayerAccountId,
                player.DisplayName,
                player.PhoneNumber))
            .ToListAsync(cancellationToken);

        return players;
    }

    /// <summary>
    /// Брони: сначала те, что ещё впереди (за ними и приходят к стойке), потом прошедшие — от
    /// свежих к старым. Отменённые не прячем: с ними как раз и приходят спорить.
    /// </summary>
    private async Task<IReadOnlyList<BranchSearchResultDto>> SearchReservationsAsync(
        Guid organizationId,
        Guid branchId,
        string normalized,
        string digits,
        int take,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var matching = dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.OrganizationId == organizationId &&
                reservation.BranchId == branchId &&
                (reservation.CustomerName.ToUpper().Contains(normalized) ||
                    (digits.Length > 0 && reservation.PhoneNumber != null &&
                        reservation.PhoneNumber
                            .Replace(" ", string.Empty)
                            .Replace("-", string.Empty)
                            .Replace("(", string.Empty)
                            .Replace(")", string.Empty)
                            .Replace("+", string.Empty)
                            .Contains(digits))));

        var upcoming = await matching
            .Where(reservation => reservation.EndsAtUtc >= now)
            .OrderBy(reservation => reservation.StartsAtUtc)
            .Take(take)
            .Select(reservation => new BranchSearchResultDto(
                BranchSearchKindNames.Reservation,
                reservation.ReservationId,
                reservation.CustomerName,
                reservation.PhoneNumber,
                reservation.StartsAtUtc))
            .ToListAsync(cancellationToken);
        if (upcoming.Count >= take)
        {
            return upcoming;
        }

        var past = await matching
            .Where(reservation => reservation.EndsAtUtc < now)
            .OrderByDescending(reservation => reservation.StartsAtUtc)
            .Take(take - upcoming.Count)
            .Select(reservation => new BranchSearchResultDto(
                BranchSearchKindNames.Reservation,
                reservation.ReservationId,
                reservation.CustomerName,
                reservation.PhoneNumber,
                reservation.StartsAtUtc))
            .ToListAsync(cancellationToken);

        return [.. upcoming, .. past];
    }

    /// <summary>
    /// Чек ищется по номеру и только по нему: с чеком приходят через неделю, и в руках у
    /// человека именно номер. Ограничения по дате нет — спор о старой покупке тем и живёт.
    /// </summary>
    private async Task<IReadOnlyList<BranchSearchResultDto>> SearchReceiptsAsync(
        Guid organizationId,
        Guid branchId,
        string normalized,
        int take,
        CancellationToken cancellationToken)
    {
        return await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt =>
                receipt.OrganizationId == organizationId &&
                receipt.BranchId == branchId &&
                receipt.ReceiptNumber.ToUpper().Contains(normalized))
            .OrderByDescending(receipt => receipt.CreatedAtUtc)
            .Take(take)
            .Select(receipt => new BranchSearchResultDto(
                BranchSearchKindNames.Receipt,
                receipt.ReceiptId,
                receipt.ReceiptNumber,
                null,
                receipt.CreatedAtUtc,
                receipt.TotalMinorUnits,
                receipt.CurrencyCode))
            .ToListAsync(cancellationToken);
    }
}
