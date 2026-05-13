using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class EfOperatorReferenceDataService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IOperatorReferenceDataService
{
    private const int MinimumSearchLength = 2;
    private const int DefaultLimit = 20;
    private const int MaximumLimit = 50;

    public async Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (trimmedQuery.Length < MinimumSearchLength)
        {
            return [];
        }

        var normalizedQuery = trimmedQuery.ToUpperInvariant();
        var take = limit <= 0
            ? DefaultLimit
            : Math.Min(limit, MaximumLimit);

        var players = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(player =>
                player.OrganizationId == organizationId &&
                player.HomeBranchId == branchId &&
                player.IsActive)
            .Where(player =>
                player.DisplayName.ToUpper().Contains(normalizedQuery) ||
                (player.PhoneNumber != null && player.PhoneNumber.Contains(trimmedQuery)))
            .OrderBy(player => player.DisplayName)
            .ThenBy(player => player.PlayerAccountId)
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
        var packageCounts = await dbContext.PlayerPackages
            .AsNoTracking()
            .Where(package =>
                package.OrganizationId == organizationId &&
                package.BranchId == branchId &&
                playerIds.Contains(package.PlayerAccountId) &&
                (package.ExpiresAtUtc == null || package.ExpiresAtUtc > now))
            .GroupBy(package => package.PlayerAccountId)
            .Select(group => new
            {
                PlayerAccountId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
        var packageCountLookup = packageCounts.ToDictionary(
            package => package.PlayerAccountId,
            package => package.Count);

        return players
            .Select(player => new PlayerSearchResultDto(
                player.PlayerAccountId,
                player.DisplayName,
                player.PhoneNumber,
                GetBalance(balanceLookup, player.PlayerAccountId, LedgerAccountTypeNames.Wallet),
                GetBalance(balanceLookup, player.PlayerAccountId, LedgerAccountTypeNames.Debt),
                packageCountLookup.GetValueOrDefault(player.PlayerAccountId),
                player.IsActive))
            .ToList();
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
                row.Version.RoundingIncrementMinutes))
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
