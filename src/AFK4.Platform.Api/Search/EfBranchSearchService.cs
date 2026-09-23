using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Search;

/// <summary>
/// Поиск по филиалу для палитры оператора: места, клиенты, брони, чеки и заказы одной строкой.
///
/// Каждый вид ищется по тому, чем его называют вслух: место — по имени ПК, клиент — по имени и
/// телефону, бронь — по имени и телефону гостя, чек — по номеру, заказ — по номеру, гостю и месту.
/// Идентификаторов в поиске нет намеренно: guid в палитре никто не набирает.
/// </summary>
public sealed class EfBranchSearchService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IBranchSearchService
{
    // Одна буква совпала бы с половиной базы и гоняла бы запрос впустую на каждом нажатии.
    private const int MinimumQueryLength = 2;

    private const int DefaultPerKindLimit = 5;

    private const int MaximumPerKindLimit = 10;

    // Чек продажи, а не возврата: номер заказа — тот, что выбит при оформлении. У отменённого
    // заказа есть ещё и чек возврата, и без этого условия заказ нашёлся бы дважды.
    private const string SaleReceiptType = "sale";

    private static readonly string[] OpenOrderStatuses = [ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted];

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

        if (scope.Orders)
        {
            results.AddRange(await SearchOrdersAsync(organizationId, branchId, normalized, take, cancellationToken));
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

    /// <summary>
    /// Заказ бара называют вслух тремя способами: «сорок второй» (номер его чека — другого номера
    /// у заказа нет), «Карим заказывал» и «на двенадцатый». Сначала те, что ещё в работе, в том же
    /// порядке, что и лента на стойке, — с ними подходят прямо сейчас; потом закрытые, от свежих к
    /// старым: с выданным и отменённым приходят спорить. Ограничения по дате нет, как и у чеков.
    /// </summary>
    private async Task<IReadOnlyList<BranchSearchResultDto>> SearchOrdersAsync(
        Guid organizationId,
        Guid branchId,
        string normalized,
        int take,
        CancellationToken cancellationToken)
    {
        var matching =
            from order in dbContext.ShopOrders.AsNoTracking()
            join seatRow in dbContext.Seats.AsNoTracking() on order.SeatId equals seatRow.SeatId into seatMatches
            from seat in seatMatches.DefaultIfEmpty()
            join playerRow in dbContext.PlayerAccounts.AsNoTracking()
                on order.PlayerAccountId equals playerRow.PlayerAccountId into playerMatches
            from player in playerMatches.DefaultIfEmpty()
            join receiptRow in dbContext.Receipts.AsNoTracking()
                    .Where(receipt => receipt.ReceiptType == SaleReceiptType)
                on order.PosSaleId equals receiptRow.PosSaleId into receiptMatches
            from receipt in receiptMatches.DefaultIfEmpty()
            where order.OrganizationId == organizationId
                && order.BranchId == branchId
                && ((seat != null && seat.Name.ToUpper().Contains(normalized))
                    || (player != null && player.DisplayName.ToUpper().Contains(normalized))
                    || (receipt != null && receipt.ReceiptNumber.ToUpper().Contains(normalized)))
            select new
            {
                order.ShopOrderId,
                SeatName = seat != null ? seat.Name : null,
                PlayerName = player != null ? player.DisplayName : null,
                ReceiptNumber = receipt != null ? receipt.ReceiptNumber : null,
                order.Status,
                order.PlacedAtUtc,
                order.TotalMinorUnits,
                order.CurrencyCode
            };

        var open = await matching
            .Where(order => OpenOrderStatuses.Contains(order.Status))
            .OrderBy(order => order.PlacedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
        var closed = open.Count >= take
            ? []
            : await matching
                .Where(order => !OpenOrderStatuses.Contains(order.Status))
                .OrderByDescending(order => order.PlacedAtUtc)
                .Take(take - open.Count)
                .ToListAsync(cancellationToken);

        // Заголовок — место: туда заказ несут, и так же он подписан в ленте. Место могли убрать с
        // карты зала — тогда узнаём заказ по гостю.
        return [.. open.Concat(closed).Select(order =>
        {
            var guest = string.IsNullOrWhiteSpace(order.PlayerName) ? null : order.PlayerName;
            return new BranchSearchResultDto(
                BranchSearchKindNames.Order,
                order.ShopOrderId,
                order.SeatName ?? guest ?? string.Empty,
                order.SeatName is null ? null : guest,
                order.PlacedAtUtc,
                order.TotalMinorUnits,
                order.CurrencyCode,
                order.Status,
                order.ReceiptNumber);
        })];
    }
}
