using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed record PackageRemainingSeconds(int IncludedSeconds, int BonusSeconds);

/// <summary>Деньги игрока в одном клубе: доступно, придержано под брони, должен.</summary>
public sealed record ClubBalances(long WalletMinorUnits, long HeldMinorUnits, long DebtMinorUnits);

public static class LedgerBalanceProjector
{
    private const string DefaultCurrencyCode = "TJS";

    public static async Task<WalletSummaryDto?> GetWalletSummaryAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var player = await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlayerAccountId == playerAccountId, cancellationToken);

        if (player is null)
        {
            return null;
        }

        var currencyCodes = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (currencyCodes.Count > 1)
        {
            throw new InvalidOperationException(
                $"Cannot project wallet summary for player account '{playerAccountId}' because ledger entries contain multiple currencies.");
        }

        var currencyCode = currencyCodes.SingleOrDefault() ?? DefaultCurrencyCode;

        var entries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.LedgerEntryId)
            .Take(25)
            .ToListAsync(cancellationToken);

        var balances = await GetClubBalancesAsync(dbContext, playerAccountId, cancellationToken);

        return new WalletSummaryDto(
            playerAccountId,
            new MoneyDto(currencyCode, balances.WalletMinorUnits),
            new MoneyDto(currencyCode, balances.HeldMinorUnits),
            new MoneyDto(currencyCode, balances.DebtMinorUnits),
            entries.Select(ToDto).ToList());
    }

    /// <summary>
    /// Три числа кошелька одним обращением. Остаток — по-прежнему то, что можно потратить: холд
    /// уже вычтен из него, потому что холд и есть отрицательная запись журнала. «Придержано» —
    /// не четвёртое место хранения денег, а объяснение, куда делась часть остатка.
    /// </summary>
    public static async Task<ClubBalances> GetClubBalancesAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var wallet = await SumAmountAsync(dbContext, playerAccountId, LedgerAccountTypeNames.Wallet, cancellationToken);
        var debt = await SumAmountAsync(dbContext, playerAccountId, LedgerAccountTypeNames.Debt, cancellationToken);
        var held = await SumUnreleasedHoldsAsync(dbContext, playerAccountId, cancellationToken);

        return new ClubBalances(wallet, held, debt);
    }

    /// <summary>
    /// Остатки сразу по нескольким пакетам — одним запросом.
    ///
    /// Поштучный опрос стоил двух обращений к базе на каждый купленный пакет: экран «Пакеты»
    /// открывался за два десятка запросов у того, кто давно ходит в клуб.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, PackageRemainingSeconds>> GetPackageRemainingSecondsAsync(
        PlatformDbContext dbContext,
        IReadOnlyCollection<Guid> playerPackageIds,
        CancellationToken cancellationToken)
    {
        if (playerPackageIds.Count == 0)
        {
            return new Dictionary<Guid, PackageRemainingSeconds>();
        }

        var sums = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerPackageId != null &&
                playerPackageIds.Contains(entry.PlayerPackageId.Value))
            .GroupBy(entry => new { PackageId = entry.PlayerPackageId!.Value, entry.AccountType })
            .Select(group => new
            {
                group.Key.PackageId,
                group.Key.AccountType,
                Seconds = group.Sum(entry => (int?)entry.QuantitySeconds) ?? 0
            })
            .ToListAsync(cancellationToken);

        var byPackage = sums
            .GroupBy(row => row.PackageId)
            .ToDictionary(
                group => group.Key,
                group => new PackageRemainingSeconds(
                    group.Where(row => row.AccountType == LedgerAccountTypeNames.PackageTime)
                        .Sum(row => row.Seconds),
                    group.Where(row => row.AccountType == LedgerAccountTypeNames.BonusTime)
                        .Sum(row => row.Seconds)));

        return playerPackageIds.ToDictionary(
            id => id,
            id => byPackage.TryGetValue(id, out var remaining) ? remaining : new PackageRemainingSeconds(0, 0));
    }

    public static async Task<PackageRemainingSeconds> GetPackageRemainingSecondsAsync(
        PlatformDbContext dbContext,
        Guid playerPackageId,
        CancellationToken cancellationToken)
    {
        var included = await SumQuantityAsync(dbContext, playerPackageId, LedgerAccountTypeNames.PackageTime, cancellationToken);
        var bonus = await SumQuantityAsync(dbContext, playerPackageId, LedgerAccountTypeNames.BonusTime, cancellationToken);

        return new PackageRemainingSeconds(included, bonus);
    }

    public static LedgerEntryDto ToDto(LedgerEntryEntity entry)
    {
        return new LedgerEntryDto(
            entry.LedgerEntryId,
            entry.OrganizationId,
            entry.BranchId,
            entry.PlayerAccountId,
            entry.SessionId,
            entry.PlayerPackageId,
            entry.EntryType,
            entry.AccountType,
            new MoneyDto(entry.CurrencyCode, entry.AmountMinorUnits),
            entry.QuantitySeconds,
            entry.Description,
            entry.Reason,
            entry.ReversesLedgerEntryId,
            entry.CreatedByStaffUserId,
            entry.CreatedAtUtc);
    }

    /// <summary>
    /// Заморожено под непогашенные брони. Снятый холд — это реверс, поэтому в сумму он не входит.
    /// Число положительное: в журнале холд отрицателен, но человеку показывают «придержано 50»,
    /// а не «придержано минус 50».
    /// </summary>
    private static async Task<long> SumUnreleasedHoldsAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var released = dbContext.LedgerEntries
            .Where(entry => entry.ReversesLedgerEntryId != null)
            .Select(entry => entry.ReversesLedgerEntryId!.Value);

        var held = await dbContext.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerAccountId
                && entry.EntryType == LedgerEntryTypeNames.ReservationHold
                && !released.Contains(entry.LedgerEntryId))
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;

        return -held;
    }

    private static async Task<long> SumAmountAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string accountType,
        CancellationToken cancellationToken)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerAccountId && entry.AccountType == accountType)
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
    }

    private static async Task<int> SumQuantityAsync(
        PlatformDbContext dbContext,
        Guid playerPackageId,
        string accountType,
        CancellationToken cancellationToken)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.PlayerPackageId == playerPackageId && entry.AccountType == accountType)
            .SumAsync(entry => (int?)entry.QuantitySeconds, cancellationToken) ?? 0;
    }
}
