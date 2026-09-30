using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Search;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class EfOperatorReferenceDataService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider)
{
    private const int DefaultLimit = 20;
    // Справочник клиентов просит страницу в 50 плюс одну: лишняя строка говорит, что страница не
    // последняя, и ответ не нужно оборачивать в конверт ради одного флага.
    private const int MaximumLimit = 100;

    // Окна поиска и справочник клиентов (вкладка «Клиенты») спрашивают одно и то же, но справочнику
    // нужен и пустой запрос («покажи всех»), и отбор, и страницы — пустой запрос раньше отдавал [],
    // и экран «Клиенты» был пуст у каждого клуба.
    public async Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        int limit,
        bool includeInactive,
        CancellationToken cancellationToken,
        string? segment = null,
        int offset = 0)
    {
        var take = limit <= 0
            ? DefaultLimit
            : Math.Min(limit, MaximumLimit);

        var matching = MatchingPlayers(organizationId, branchId, query, includeInactive || segment == PlayerDirectorySegments.Inactive);
        if (segment == PlayerDirectorySegments.Inactive)
        {
            matching = matching.Where(player => !player.IsActive);
        }
        else if (segment == PlayerDirectorySegments.Debt)
        {
            var debtors = dbContext.LedgerEntries
                .Where(entry =>
                    entry.OrganizationId == organizationId &&
                    entry.BranchId == branchId &&
                    entry.AccountType == LedgerAccountTypeNames.Debt)
                .GroupBy(entry => entry.PlayerAccountId)
                .Where(group => group.Sum(entry => entry.AmountMinorUnits) > 0)
                .Select(group => group.Key);
            matching = matching.Where(player => debtors.Contains(player.PlayerAccountId));
        }

        // ponytail: offset-страницы — на тысячах клиентов хватает; курсор по (имя, id), если дойдёт до сотен тысяч.
        var players = await matching
            .OrderBy(player => player.DisplayName)
            .ThenBy(player => player.PlayerAccountId)
            .Skip(Math.Max(0, offset))
            .Take(take)
            .ToListAsync(cancellationToken);

        if (players.Count == 0)
        {
            return [];
        }

        var playerIds = players
            .Select(player => player.PlayerAccountId)
            .ToHashSet();

        var balances = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                playerIds.Contains(entry.PlayerAccountId) &&
                (entry.AccountType == LedgerAccountTypeNames.Wallet ||
                    entry.AccountType == LedgerAccountTypeNames.Debt))
            .GroupBy(entry => new { entry.PlayerAccountId, entry.AccountType })
            .Select(group => new
            {
                group.Key.PlayerAccountId,
                group.Key.AccountType,
                Balance = group.Sum(entry => entry.AmountMinorUnits)
            })
            .ToListAsync(cancellationToken);
        var balanceLookup = balances.ToDictionary(
            balance => (balance.PlayerAccountId, balance.AccountType),
            balance => balance.Balance);

        var now = timeProvider.GetUtcNow();
        var activePackages = await dbContext.PlayerPackages
            .AsNoTracking()
            .Where(package =>
                package.OrganizationId == organizationId &&
                package.BranchId == branchId &&
                playerIds.Contains(package.PlayerAccountId) &&
                (package.ExpiresAtUtc == null || package.ExpiresAtUtc > now))
            .Select(package => new
            {
                package.PlayerAccountId,
                package.PlayerPackageId,
                package.Name
            })
            .ToListAsync(cancellationToken);
        var packageCountLookup = activePackages
            .GroupBy(package => package.PlayerAccountId)
            .ToDictionary(group => group.Key, group => group.Count());

        var packageIds = activePackages
            .Select(package => package.PlayerPackageId)
            .ToList();
        var remainingSecondsLookup = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.PlayerPackageId != null &&
                packageIds.Contains(entry.PlayerPackageId.Value) &&
                (entry.AccountType == LedgerAccountTypeNames.PackageTime ||
                    entry.AccountType == LedgerAccountTypeNames.BonusTime))
            .GroupBy(entry => entry.PlayerPackageId!.Value)
            .Select(group => new
            {
                PlayerPackageId = group.Key,
                Seconds = group.Sum(entry => entry.QuantitySeconds)
            })
            .ToDictionaryAsync(x => x.PlayerPackageId, x => x.Seconds, cancellationToken);

        var bestPackageLookup = activePackages
            .Select(package => new
            {
                package.PlayerAccountId,
                package.Name,
                RemainingSeconds = remainingSecondsLookup.GetValueOrDefault(package.PlayerPackageId)
            })
            .Where(package => package.RemainingSeconds > 0)
            .GroupBy(package => package.PlayerAccountId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(package => package.RemainingSeconds).First());

        var lastActivityLookup = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => playerIds.Contains(session.PlayerAccountId!.Value))
            .Select(session => new
            {
                session.PlayerAccountId,
                EffectiveAt = session.EndedAtUtc ?? session.StartedAtUtc ?? session.RequestedAtUtc
            })
            .GroupBy(session => session.PlayerAccountId)
            .Select(group => new
            {
                PlayerAccountId = group.Key,
                Last = group.Max(session => session.EffectiveAt)
            })
            .ToDictionaryAsync(x => x.PlayerAccountId!.Value, x => x.Last, cancellationToken);

        // День рождения — у личности, а не у карточки: гость ввёл его в приложении один раз для всей сети.
        var personIds = players.Where(player => player.PlatformPersonId != null).Select(player => player.PlatformPersonId!.Value).ToList();
        var birthDateLookup = personIds.Count == 0
            ? new Dictionary<Guid, DateOnly>()
            : await dbContext.PlatformPersons
                .AsNoTracking()
                .Where(person => personIds.Contains(person.PlatformPersonId) && person.BirthDate != null)
                .ToDictionaryAsync(person => person.PlatformPersonId, person => person.BirthDate!.Value, cancellationToken);

        return players
            .Select(player =>
            {
                var bestPackage = bestPackageLookup.GetValueOrDefault(player.PlayerAccountId);
                return new PlayerSearchResultDto(
                    player.PlayerAccountId,
                    player.DisplayName,
                    player.PhoneNumber,
                    GetBalance(balanceLookup, player.PlayerAccountId, LedgerAccountTypeNames.Wallet),
                    GetBalance(balanceLookup, player.PlayerAccountId, LedgerAccountTypeNames.Debt),
                    packageCountLookup.GetValueOrDefault(player.PlayerAccountId),
                    player.IsActive,
                    CreatedAtUtc: player.CreatedAtUtc,
                    LastActivityAtUtc: lastActivityLookup.TryGetValue(player.PlayerAccountId, out var last) ? last : (DateTimeOffset?)null,
                    ActivePackageName: bestPackage?.Name,
                    ActivePackageRemainingMinutes: bestPackage?.RemainingSeconds / 60 ?? 0,
                    PlatformPersonId: player.PlatformPersonId,
                    CreatedFromApp: player.CreatedFromApp,
                    BirthDate: player.PlatformPersonId is { } personId && birthDateLookup.TryGetValue(personId, out var birthDate)
                        ? birthDate
                        : null);
            })
            .ToList();
    }

    /// <summary>
    /// Итоги по всем клиентам, подходящим под запрос, а не по той странице, что открыта на экране:
    /// у клуба с сотнями клиентов итог «по первым пятидесяти» молча врал.
    /// </summary>
    public async Task<PlayersSummaryDto> GetPlayersSummaryAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        CancellationToken cancellationToken)
    {
        var players = MatchingPlayers(organizationId, branchId, query, includeInactive: true);
        var totalCount = await players.CountAsync(cancellationToken);
        var inactiveCount = await players.CountAsync(player => !player.IsActive, cancellationToken);

        // Сумма по клиенту, а не по записи: у одного кошелёк в плюсе, у другого долг — и минус одного
        // не гасит плюс другого. Как и в строке списка, отрицательный остаток в итог не идёт.
        var perPlayer = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                (entry.AccountType == LedgerAccountTypeNames.Wallet ||
                    entry.AccountType == LedgerAccountTypeNames.Debt))
            .Join(players, entry => entry.PlayerAccountId, player => player.PlayerAccountId, (entry, _) => entry)
            .GroupBy(entry => new { entry.PlayerAccountId, entry.AccountType })
            .Select(group => new { group.Key.AccountType, Balance = group.Sum(entry => entry.AmountMinorUnits) })
            .ToListAsync(cancellationToken);

        var walletTotal = perPlayer
            .Where(row => row.AccountType == LedgerAccountTypeNames.Wallet && row.Balance > 0)
            .Sum(row => row.Balance);
        var debts = perPlayer
            .Where(row => row.AccountType == LedgerAccountTypeNames.Debt && row.Balance > 0)
            .ToList();

        return new PlayersSummaryDto(totalCount, debts.Count, inactiveCount, walletTotal, debts.Sum(row => row.Balance));
    }

    private IQueryable<PlayerAccountEntity> MatchingPlayers(
        Guid organizationId,
        Guid branchId,
        string? query,
        bool includeInactive)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        var normalizedQuery = trimmedQuery.ToUpperInvariant();
        var digits = PhoneQuery.Digits(trimmedQuery);

        var players = dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(player =>
                player.OrganizationId == organizationId &&
                player.HomeBranchId == branchId &&
                (includeInactive || player.IsActive));
        if (trimmedQuery.Length == 0)
        {
            return players;
        }

        // Номер набирают подряд цифрами, а записан он с пробелами и дефисами — сравниваем
        // цифры с цифрами, иначе поиск по телефону молча не находит никого (см. PhoneQuery).
        return players.Where(player =>
            player.DisplayName.ToUpper().Contains(normalizedQuery) ||
            (digits.Length > 0 && player.PhoneNumber != null &&
                player.PhoneNumber
                    .Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("(", string.Empty)
                    .Replace(")", string.Empty)
                    .Replace("+", string.Empty)
                    .Contains(digits)));
    }

    public async Task<IReadOnlyList<TariffOptionDto>> GetTariffOptionsAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await dbContext.Tariffs
            .AsNoTracking()
            .Join(
                dbContext.TariffVersions.AsNoTracking(),
                tariff => tariff.TariffId,
                version => version.TariffId,
                (tariff, version) => new { Tariff = tariff, Version = version })
            .Where(row =>
                row.Tariff.OrganizationId == organizationId &&
                row.Tariff.BranchId == branchId &&
                row.Tariff.IsActive &&
                row.Version.OrganizationId == organizationId &&
                row.Version.BranchId == branchId &&
                row.Version.EffectiveFromUtc <= now &&
                (row.Version.RetiredAtUtc == null || row.Version.RetiredAtUtc > now))
            .OrderBy(row => row.Tariff.Name)
            .ThenByDescending(row => row.Version.EffectiveFromUtc)
            .ThenByDescending(row => row.Version.VersionNumber)
            .ToListAsync(cancellationToken);

        // Часовой пояс филиала, а не сервера и не телефона: окно владелец задавал по времени
        // своего клуба, и «действует ли сейчас» имеет ответ только в нём.
        var timeZoneId = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && branch.BranchId == branchId)
            .Select(branch => branch.PreferredTimeZone)
            .SingleOrDefaultAsync(cancellationToken);
        var zone = BranchLocalTime.ResolveZone(timeZoneId ?? "UTC");

        return rows
            .GroupBy(row => row.Tariff.TariffId)
            .Select(group => group.First())
            .OrderBy(row => row.Tariff.Name)
            .Select(row => new TariffOptionDto(
                row.Tariff.TariffId,
                row.Version.TariffVersionId,
                row.Tariff.Name,
                row.Version.TariffVersionId.ToString("D"),
                row.Version.VersionNumber,
                row.Version.CurrencyCode,
                row.Version.PricePerMinuteMinorUnits,
                row.Version.MinimumBillableMinutes,
                row.Version.RoundingIncrementMinutes,
                row.Version.EffectiveFromUtc,
                row.Tariff.AppliesOnDaysMask,
                row.Tariff.AppliesFromMinuteOfDay,
                row.Tariff.AppliesToMinuteOfDay,
                // Тариф вне часов не прячется: пропавший из списка «Утренний» читается как сбой,
                // а названный с его часами объясняет и себя, и почему сейчас недоступен.
                TariffSchedule.AppliesAt(
                    row.Tariff.AppliesOnDaysMask,
                    row.Tariff.AppliesFromMinuteOfDay,
                    row.Tariff.AppliesToMinuteOfDay,
                    now,
                    zone),
                row.Tariff.FeaturedOnPcs))
            .ToList();
    }

    public async Task<IReadOnlyList<PackageOptionDto>> GetPackageOptionsAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        return await dbContext.PackageDefinitions
            .AsNoTracking()
            .Where(package =>
                package.OrganizationId == organizationId &&
                package.BranchId == branchId &&
                package.IsActive)
            .OrderBy(package => package.Name)
            .ThenBy(package => package.PackageDefinitionId)
            .Select(package => new PackageOptionDto(
                package.PackageDefinitionId,
                package.Name,
                package.CurrencyCode,
                package.PriceMinorUnits,
                package.IncludedSeconds,
                package.BonusSeconds,
                package.ExpiresAfterDays))
            .ToListAsync(cancellationToken);
    }

    private static long GetBalance(
        IReadOnlyDictionary<(Guid PlayerAccountId, string AccountType), long> balanceLookup,
        Guid playerAccountId,
        string accountType)
    {
        return balanceLookup.TryGetValue((playerAccountId, accountType), out var balance)
            ? balance
            : 0;
    }
}
