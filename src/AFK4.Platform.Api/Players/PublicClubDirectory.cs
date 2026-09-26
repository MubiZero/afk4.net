using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AFK4.Platform.Api.Players;

public sealed class PublicClubDirectoryOptions
{
    /// <summary>
    /// Сколько держать общий список клубов (без поиска). Его открывает каждый игрок, выбирающий клуб,
    /// и все они получают одно и то же — считать заново для каждого незачем. Цена — «свободных мест»
    /// может отставать на эти секунды. Ноль — не кэшировать (тесты: у каждого своя база).
    /// </summary>
    public TimeSpan ListCacheDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Каталог клубов для мобильного приложения: у него нет поддомена, из которого веб-сборка берёт
/// организацию, а вход без неё невозможен (игрок опознаётся парой организация+телефон). Отдаём витрину
/// и только её: статус, подписка, долги и лимиты сюда не попадают.
/// </summary>
public static class PublicClubDirectory
{
    private const int MaxResults = 50;
    private const string ListCacheKey = "public-club-directory:list";

    public static async Task<IReadOnlyList<OrganizationDirectoryEntryDto>> GetAsync(
        PlatformDbContext dbContext,
        IMemoryCache cache,
        PublicClubDirectoryOptions options,
        string? query,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim();
        // Поиск не кэшируется: строк поиска бесконечно много, и память сервера раздувалась бы от
        // случайных запросов. Горячий путь — общий список, его и держим.
        if (!string.IsNullOrEmpty(trimmed) || options.ListCacheDuration <= TimeSpan.Zero)
        {
            return await BuildAsync(dbContext, trimmed, now, cancellationToken);
        }

        if (cache.TryGetValue(ListCacheKey, out IReadOnlyList<OrganizationDirectoryEntryDto>? cached) && cached is not null)
        {
            return cached;
        }

        var built = await BuildAsync(dbContext, null, now, cancellationToken);
        cache.Set(ListCacheKey, built, options.ListCacheDuration);
        return built;
    }

    private static async Task<IReadOnlyList<OrganizationDirectoryEntryDto>> BuildAsync(
        PlatformDbContext dbContext,
        string? query,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var organizations = dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Status == "active");

        if (!string.IsNullOrEmpty(query))
        {
            // ToLower().Contains(), а не EF.Functions.ILike: ILike — расширение Npgsql, и на
            // InMemory (где идут эти тесты) оно роняет запрос в 500. Каталог отдаёт максимум
            // 50 строк, так что отсутствие индексного поиска здесь ничего не стоит.
            var needle = query.ToLowerInvariant();
            organizations = organizations.Where(o =>
                o.Name.ToLower().Contains(needle) || o.Slug.ToLower().Contains(needle));
        }

        // Порядок по имени, а не по времени создания: список должен выглядеть одинаково при
        // каждом открытии, иначе выбранный глазом клуб уезжает под пальцем.
        var found = await organizations
            .OrderBy(o => o.Name)
            .Take(MaxResults)
            .Select(o => new { o.OrganizationId, o.Slug, o.Name, o.LogoUrl, o.AccentColor })
            .ToListAsync(cancellationToken);

        var organizationIds = found.Select(o => o.OrganizationId).ToList();

        // Витрина собирается отдельными запросами и склеивается в памяти, а не одним выражением с
        // группировками: клубов здесь максимум полсотни, зато запросы остаются такими, какие
        // одинаково выполняет и Postgres, и InMemory под тестами.
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(b => organizationIds.Contains(b.OrganizationId))
            .OrderBy(b => b.City).ThenBy(b => b.Name)
            .Select(b => new
            {
                b.OrganizationId, b.BranchId, b.Name, b.City, b.Address, b.Description,
                b.CoverImageUrl, b.Latitude, b.Longitude, b.WorkingHoursJson, b.PhotosJson
            })
            .ToListAsync(cancellationToken);

        // Залы с железом и числом мест — по филиалам сети.
        var branchIds = branches.Select(b => b.BranchId).ToList();
        var zones = await dbContext.Zones
            .AsNoTracking()
            .Where(zone => branchIds.Contains(zone.BranchId))
            .OrderBy(zone => zone.SortOrder)
            .Select(zone => new { zone.ZoneId, zone.BranchId, zone.Name, zone.HardwareSummary })
            .ToListAsync(cancellationToken);

        // Места одним запросом — и по залам, и по клубу целиком: раньше клубное число считалось
        // вторым запросом по тем же строкам.
        var seatCounts = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => organizationIds.Contains(seat.OrganizationId))
            .GroupBy(seat => new { seat.OrganizationId, seat.ZoneId })
            .Select(group => new { group.Key.OrganizationId, group.Key.ZoneId, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var seatsByZone = seatCounts.GroupBy(count => count.ZoneId).ToDictionary(group => group.Key, group => group.Sum(count => count.Count));
        var seatsByOrganization = seatCounts.GroupBy(count => count.OrganizationId).ToDictionary(group => group.Key, group => group.Sum(count => count.Count));

        // Сколько мест занято прямо сейчас. Игрок выбирает клуб, чтобы в него поехать, и
        // «40 мест» отвечает на другой вопрос: сорок мест бывает и в забитом зале.
        //
        // Занято = за местом идёт сессия либо место обещано чужой броне на ближайший час.
        // Ровно те же две причины, по которым клубный экран мест не даёт сесть
        // (`/api/me/branches/{id}/seats`), — третью, погасший ПК, здесь не считаем: связь
        // агента с сервером — это здоровье инфраструктуры, и мигнувшая сеть не должна
        // объявлять полупустой клуб забитым.
        var busySeats = await (
            from session in dbContext.Sessions.AsNoTracking()
            join seat in dbContext.Seats.AsNoTracking() on session.SeatId equals seat.SeatId
            where branchIds.Contains(seat.BranchId)
                && (session.State == SessionStateNames.Active
                    || session.State == SessionStateNames.Paused
                    || session.State == SessionStateNames.Ending)
            select new { seat.SeatId, seat.ZoneId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var soon = now.AddHours(1);
        var reservedSeats = await (
            from reservation in dbContext.Reservations.AsNoTracking()
            join seat in dbContext.Seats.AsNoTracking()
                on reservation.SeatId equals (Guid?)seat.SeatId
            where branchIds.Contains(seat.BranchId)
                && (reservation.State == ReservationStateNames.Confirmed
                    || reservation.State == ReservationStateNames.Pending)
                && reservation.StartsAtUtc < soon
                && reservation.EndsAtUtc > now
            select new { seat.SeatId, seat.ZoneId })
            .Distinct()
            .ToListAsync(cancellationToken);

        // Одно место могло попасть в оба списка (сел раньше своей же брони) — считаем его
        // занятым один раз, иначе свободных мест окажется меньше, чем их есть.
        var takenByZone = busySeats.Concat(reservedSeats)
            .DistinctBy(taken => taken.SeatId)
            .GroupBy(taken => taken.ZoneId)
            .ToDictionary(group => group.Key, group => group.Count());

        int FreeInZone(Guid zoneId, int seatCount) =>
            Math.Max(0, seatCount - (takenByZone.TryGetValue(zoneId, out var taken) ? taken : 0));

        var places = branches.Select(b =>
        {
            var hallZones = zones.Where(zone => zone.BranchId == b.BranchId)
                .Select(zone =>
                {
                    var seatCount = seatsByZone.GetValueOrDefault(zone.ZoneId);
                    return new ClubZoneDto(
                        zone.Name, seatCount, zone.HardwareSummary, FreeInZone(zone.ZoneId, seatCount));
                })
                .ToList();

            return new
            {
                b.OrganizationId,
                Place = new ClubPlaceDto(
                    b.BranchId, b.Name, b.City, b.Address, b.Description,
                    b.CoverImageUrl, b.Latitude, b.Longitude,
                    BranchWorkingHours.Deserialize(b.WorkingHoursJson),
                    hallZones,
                    // Обложка идёт первой: она выбрана владельцем как лицо зала, остальные —
                    // за ней, в заданном им порядке.
                    [
                        .. string.IsNullOrWhiteSpace(b.CoverImageUrl) ? Array.Empty<string>() : [b.CoverImageUrl],
                        .. BranchPhotos.Deserialize(b.PhotosJson).Select(photo => photo.Url)
                    ],
                    // Числа зала — сумма его залов-зон, а не отдельный запрос: одно и то же
                    // число, посчитанное дважды, рано или поздно разойдётся.
                    hallZones.Sum(zone => zone.SeatCount),
                    hallZones.Sum(zone => zone.FreeSeatCount))
            };
        }).ToList();

        // «Цена от» — по действующим версиям тарифов: снятые с публикации и ещё не вступившие в силу
        // обещали бы игроку цену, которую на кассе никто не назовёт. Минимум считает база: раньше
        // сюда выгружались все версии тарифов полусотни клубов, чтобы взять из них одну строку.
        var prices = await dbContext.TariffVersions
            .AsNoTracking()
            .Where(v => organizationIds.Contains(v.OrganizationId)
                && v.RetiredAtUtc == null
                && v.EffectiveFromUtc <= now)
            .GroupBy(v => new { v.OrganizationId, v.CurrencyCode })
            .Select(group => new
            {
                group.Key.OrganizationId,
                group.Key.CurrencyCode,
                PricePerMinuteMinorUnits = group.Min(v => v.PricePerMinuteMinorUnits)
            })
            .ToListAsync(cancellationToken);

        // Оценка клуба — то же, что цена и адрес: часть витрины, по которой выбирают. Считаем
        // её здесь же, одним запросом на всю страницу каталога.
        var ratings = await dbContext.ClubReviews
            .AsNoTracking()
            .Where(review => organizationIds.Contains(review.OrganizationId))
            .GroupBy(review => review.OrganizationId)
            .Select(group => new
            {
                OrganizationId = group.Key,
                Average = group.Average(review => (double)review.Rating),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return found.Select(o =>
        {
            var rating = ratings.FirstOrDefault(r => r.OrganizationId == o.OrganizationId);
            var cheapest = prices
                .Where(p => p.OrganizationId == o.OrganizationId)
                .OrderBy(p => p.PricePerMinuteMinorUnits)
                .FirstOrDefault();

            return new OrganizationDirectoryEntryDto(
                o.OrganizationId,
                o.Slug,
                o.Name,
                o.LogoUrl,
                o.AccentColor,
                places.Where(p => p.OrganizationId == o.OrganizationId)
                    .Select(p => p.Place)
                    .ToList(),
                cheapest is null ? null : cheapest.PricePerMinuteMinorUnits * 60,
                cheapest?.CurrencyCode,
                seatsByOrganization.GetValueOrDefault(o.OrganizationId),
                rating is null ? null : Math.Round(rating.Average, 1),
                rating?.Count ?? 0);
        }).ToList();
    }
}
