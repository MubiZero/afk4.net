using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AFK4.Platform.Api.Ads;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Showcase;
using AFK4.Shared.Contracts.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AFK4.Platform.Api.Showcase;

/// <summary>
/// Витрина свободного ПК (спека оболочки, §5.7): новости с отметкой «на экране ПК», выделенные
/// тарифы и товары, автокарточки — ближайший турнир, пакеты, хит бара; у бесплатного тарифа —
/// реклама платформы каждой третьей. Модуль только читает чужие таблицы; своих у витрины нет —
/// отметки живут у самих записей.
/// </summary>
public sealed class DeviceShowcase(
    PlatformDbContext db,
    IOperatorReferenceDataService referenceData,
    IOrganizationFeatureSnapshot features,
    IMemoryCache cache,
    TimeProvider clock)
{
    // Опрос агента раз в 10 минут с каждого ПК; расчёт — один на филиал в минуту.
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    public sealed record Snapshot(DeviceShowcaseDto Showcase, string ETag);

    public Task<Snapshot> GetAsync(Guid organizationId, Guid branchId, CancellationToken ct) =>
        cache.GetOrCreateAsync(("device-showcase", organizationId, branchId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var showcase = await BuildAsync(organizationId, branchId, ct);
            return new Snapshot(showcase, ETagFor(showcase));
        })!;

    public async Task<DeviceShowcaseDto> BuildAsync(Guid organizationId, Guid branchId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cards = new List<ShowcaseCardDto>();

        var news = await db.NewsItems.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId
                && item.IsPublished
                && item.ShowOnPcs
                && (item.BranchId == null || item.BranchId == branchId)
                && (item.PublishAtUtc == null || item.PublishAtUtc <= now)
                && (item.ExpiresAtUtc == null || item.ExpiresAtUtc > now))
            .OrderByDescending(item => item.PublishAtUtc ?? item.CreatedAtUtc)
            .Take(ShowcaseLimits.MaxNews)
            .Select(item => new { item.Id, item.Title, item.Body, item.ImageUrl })
            .ToListAsync(ct);
        cards.AddRange(news.Select(item => new ShowcaseCardDto(
            $"news:{item.Id:N}", ShowcaseCardKindNames.News, item.Title, Clip(item.Body), ImageUrl: item.ImageUrl)));

        // Цена — из той же действующей версии, по которой списывает касса: витрина не обещает
        // цифру, которой нет в продаже. Тариф без действующей версии не попадёт.
        var tariffs = await referenceData.GetTariffOptionsAsync(organizationId, branchId, ct);
        cards.AddRange(tariffs
            .Where(option => option.FeaturedOnPcs)
            .Take(ShowcaseLimits.MaxTariffs)
            .Select(option => new ShowcaseCardDto(
                $"tariff:{option.TariffId:N}",
                ShowcaseCardKindNames.Tariff,
                option.Name,
                Price: new MoneyDto(option.CurrencyCode, option.PricePerMinuteMinorUnits * 60),
                TimeWindow: TimeWindow(option.AppliesFromMinuteOfDay, option.AppliesToMinuteOfDay))));

        var featuredProducts = await db.PosProducts.AsNoTracking()
            .Where(product => product.OrganizationId == organizationId
                && product.BranchId == branchId
                && product.IsActive
                && product.FeaturedOnPcs)
            .OrderBy(product => product.Name)
            .Take(ShowcaseLimits.MaxProducts)
            .Select(product => new { product.ProductId, product.Name, product.CurrencyCode, product.PriceMinorUnits, product.ImageUrl })
            .ToListAsync(ct);
        cards.AddRange(featuredProducts.Select(product => new ShowcaseCardDto(
            $"product:{product.ProductId:N}",
            ShowcaseCardKindNames.Product,
            product.Name,
            ImageUrl: product.ImageUrl,
            Price: new MoneyDto(product.CurrencyCode, product.PriceMinorUnits))));

        var horizon = now.AddDays(ShowcaseLimits.TournamentHorizonDays);
        var tournament = await db.Tournaments.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId
                && item.BranchId == branchId
                && item.State == TournamentStateNames.Published
                && item.StartsAtUtc > now
                && item.StartsAtUtc <= horizon)
            .OrderBy(item => item.StartsAtUtc)
            .Select(item => new
            {
                item.TournamentId, item.Title, item.Description, item.Discipline, item.StartsAtUtc,
                item.EntryFeeMinorUnits, item.CurrencyCode
            })
            .FirstOrDefaultAsync(ct);
        if (tournament is not null)
        {
            cards.Add(new ShowcaseCardDto(
                $"tournament:{tournament.TournamentId:N}",
                ShowcaseCardKindNames.Tournament,
                tournament.Title,
                Clip(tournament.Description),
                Subtitle: string.IsNullOrWhiteSpace(tournament.Discipline) ? null : tournament.Discipline,
                // Бесплатное участие — без цены: «0 с.» на экране читается как ошибка.
                Price: tournament.EntryFeeMinorUnits > 0
                    ? new MoneyDto(tournament.CurrencyCode, tournament.EntryFeeMinorUnits)
                    : null,
                StartsAtUtc: tournament.StartsAtUtc));
        }

        var packages = (await referenceData.GetPackageOptionsAsync(organizationId, branchId, ct))
            .OrderBy(package => package.PriceMinorUnits)
            .ThenBy(package => package.Name, StringComparer.Ordinal)
            .Take(ShowcaseLimits.MaxPackages)
            .Select(package => new ShowcasePackageLineDto(
                package.Name,
                new MoneyDto(package.CurrencyCode, package.PriceMinorUnits),
                (package.IncludedSeconds + package.BonusSeconds) / 60))
            .ToList();
        if (packages.Count > 0)
        {
            cards.Add(new ShowcaseCardDto(
                $"packages:{branchId:N}", ShowcaseCardKindNames.Packages, string.Empty, Packages: packages));
        }

        var hit = await BarHitAsync(organizationId, branchId, now, featuredProducts.Select(product => product.ProductId).ToList(), ct);
        if (hit is not null)
        {
            cards.Add(hit);
        }

        var club = cards.Take(ShowcaseLimits.MaxCards).ToList();
        // Реклама платформы — только у клуба с platform_ads (бесплатный тариф), каждой третьей.
        if (!(await features.GetEnabledAsync(organizationId, ct)).Contains(PlatformFeatureNames.PlatformAds))
        {
            return new DeviceShowcaseDto(club);
        }

        var ads = await PlatformAds.CardsForBranchAsync(db, organizationId, branchId, now, ct);
        return new DeviceShowcaseDto(PlatformAds.Interleave(club, ads));
    }

    /// <summary>
    /// Больше всего продано за месяц оплаченными чеками. Товар, который уже выделен клубом, хитом
    /// не повторяется — иначе одна бутылка заняла бы две карточки подряд.
    /// </summary>
    private async Task<ShowcaseCardDto?> BarHitAsync(
        Guid organizationId, Guid branchId, DateTimeOffset now, IReadOnlyCollection<Guid> alreadyShown, CancellationToken ct)
    {
        var since = now.AddDays(-ShowcaseLimits.BarHitWindowDays);
        var leaders = await (
                from line in db.PosSaleLines.AsNoTracking()
                join sale in db.PosSales.AsNoTracking() on line.PosSaleId equals sale.PosSaleId
                where sale.OrganizationId == organizationId
                      && sale.BranchId == branchId
                      && sale.State == PosSaleStateNames.Paid
                      && sale.PaidAtUtc >= since
                group line by line.ProductId into sold
                select new { ProductId = sold.Key, Units = sold.Sum(line => line.Quantity) })
            .Where(row => row.Units >= ShowcaseLimits.BarHitMinimumUnits)
            .OrderByDescending(row => row.Units)
            .ThenBy(row => row.ProductId)
            .Take(ShowcaseLimits.MaxProducts + 1)
            .ToListAsync(ct);
        foreach (var leader in leaders)
        {
            if (alreadyShown.Contains(leader.ProductId)) continue;
            var product = await db.PosProducts.AsNoTracking()
                .Where(candidate => candidate.ProductId == leader.ProductId
                    && candidate.OrganizationId == organizationId
                    && candidate.BranchId == branchId
                    && candidate.IsActive)
                .Select(candidate => new { candidate.Name, candidate.CurrencyCode, candidate.PriceMinorUnits, candidate.ImageUrl })
                .FirstOrDefaultAsync(ct);
            // Снятый с продажи товар не зовёт к стойке за тем, чего нет, — хитом становится следующий.
            if (product is null) continue;
            return new ShowcaseCardDto(
                $"bar_hit:{leader.ProductId:N}",
                ShowcaseCardKindNames.BarHit,
                product.Name,
                ImageUrl: product.ImageUrl,
                Price: new MoneyDto(product.CurrencyCode, product.PriceMinorUnits));
        }

        return null;
    }

    public static string? Clip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (trimmed.Length <= ShowcaseLimits.MaxBodyLength) return trimmed;
        var cut = trimmed[..ShowcaseLimits.MaxBodyLength];
        var lastSpace = cut.LastIndexOf(' ');
        // Режем по слову, если слово не съедает больше трети; иначе — по символу.
        if (lastSpace > ShowcaseLimits.MaxBodyLength * 2 / 3) cut = cut[..lastSpace];
        return cut.TrimEnd(' ', ',', '.', ';', ':', '—', '-') + "…";
    }

    public static string? TimeWindow(int? fromMinute, int? toMinute) =>
        fromMinute is { } from && toMinute is { } to
            ? string.Create(CultureInfo.InvariantCulture, $"{from / 60:00}:{from % 60:00}–{to / 60:00}:{to % 60:00}")
            : null;

    public static string ETagFor(DeviceShowcaseDto showcase)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(showcase);
        return $"\"{Convert.ToHexStringLower(SHA256.HashData(bytes))[..32]}\"";
    }
}
