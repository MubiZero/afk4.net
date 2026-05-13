using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed record PackageRemainingSeconds(int IncludedSeconds, int BonusSeconds);

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

        var wallet = await SumAmountAsync(dbContext, playerAccountId, LedgerAccountTypeNames.Wallet, cancellationToken);
        var debt = await SumAmountAsync(dbContext, playerAccountId, LedgerAccountTypeNames.Debt, cancellationToken);

        return new WalletSummaryDto(
            playerAccountId,
            new MoneyDto(currencyCode, wallet),
            new MoneyDto(currencyCode, debt),
            entries.Select(ToDto).ToList());
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
