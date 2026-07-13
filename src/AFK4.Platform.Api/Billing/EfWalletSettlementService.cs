using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class EfWalletSettlementService(PlatformDbContext dbContext) : IWalletSettlementService
{
    public async Task<WalletSettlementResult> DebitAsync(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        Guid shiftId,
        long amountMinorUnits,
        string currencyCode,
        string description,
        string reason,
        Guid actorStaffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (amountMinorUnits <= 0)
        {
            return WalletSettlementResult.Reject("invalid_amount");
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return WalletSettlementResult.Reject("currency_required");
        }

        var playerExists = await dbContext.PlayerAccounts
            .AsNoTracking()
            .AnyAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.HomeBranchId == branchId &&
                    player.PlayerAccountId == playerAccountId,
                cancellationToken);
        if (!playerExists)
        {
            return WalletSettlementResult.Reject("player_not_found");
        }

        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        var rawCurrencies = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .ToListAsync(cancellationToken);
        var ledgerCurrencies = rawCurrencies
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ledgerCurrencies.Count > 1)
        {
            return WalletSettlementResult.Reject("ledger_currency_conflict");
        }

        var ledgerCurrency = ledgerCurrencies.SingleOrDefault();
        if (ledgerCurrency is not null &&
            !string.Equals(ledgerCurrency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return WalletSettlementResult.Reject("currency_mismatch");
        }

        var balance = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                entry.PlayerAccountId == playerAccountId &&
                entry.AccountType == LedgerAccountTypeNames.Wallet &&
                entry.CurrencyCode.ToUpper() == normalizedCurrency)
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
        if (balance < amountMinorUnits)
        {
            return WalletSettlementResult.Reject("insufficient_funds");
        }

        var debit = BillingEntryFactory.Create(
            organizationId,
            branchId,
            playerAccountId,
            sessionId,
            playerPackageId: null,
            LedgerEntryTypeNames.WalletPayment,
            LedgerAccountTypeNames.Wallet,
            -amountMinorUnits,
            quantitySeconds: 0,
            normalizedCurrency,
            description,
            reason,
            reversesLedgerEntryId: null,
            actorStaffUserId,
            now,
            shiftId);

        dbContext.LedgerEntries.Add(debit);
        return WalletSettlementResult.Ok(debit);
    }

    public async Task<WalletSettlementResult> ReverseAsync(
        LedgerEntryEntity originalDebit,
        Guid actorStaffUserId,
        string description,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (originalDebit.EntryType != LedgerEntryTypeNames.WalletPayment ||
            originalDebit.AccountType != LedgerAccountTypeNames.Wallet ||
            originalDebit.AmountMinorUnits >= 0 ||
            originalDebit.AmountMinorUnits == long.MinValue)
        {
            return WalletSettlementResult.Reject("invalid_original_debit");
        }

        var stagedReversalExists = dbContext.ChangeTracker
            .Entries<LedgerEntryEntity>()
            .Any(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.ReversesLedgerEntryId == originalDebit.LedgerEntryId);
        if (stagedReversalExists)
        {
            return WalletSettlementResult.Reject("already_reversed");
        }

        var persistedReversalExists = await dbContext.LedgerEntries
            .AsNoTracking()
            .AnyAsync(
                entry => entry.ReversesLedgerEntryId == originalDebit.LedgerEntryId,
                cancellationToken);
        if (persistedReversalExists)
        {
            return WalletSettlementResult.Reject("already_reversed");
        }

        var reversal = BillingEntryFactory.Create(
            originalDebit.OrganizationId,
            originalDebit.BranchId,
            originalDebit.PlayerAccountId,
            originalDebit.SessionId,
            originalDebit.PlayerPackageId,
            LedgerEntryTypeNames.Reversal,
            LedgerAccountTypeNames.Wallet,
            -originalDebit.AmountMinorUnits,
            -originalDebit.QuantitySeconds,
            originalDebit.CurrencyCode,
            description,
            reason,
            originalDebit.LedgerEntryId,
            actorStaffUserId,
            now,
            originalDebit.ShiftId);

        dbContext.LedgerEntries.Add(reversal);
        return WalletSettlementResult.Ok(reversal);
    }
}
