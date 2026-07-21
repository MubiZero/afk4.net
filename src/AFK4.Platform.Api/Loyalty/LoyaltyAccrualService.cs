using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Loyalty;

public sealed class LoyaltyAccrualService(PlatformDbContext dbContext) : ILoyaltyAccrualService
{
    public async Task<LedgerEntryEntity?> BuildCashbackEntryAsync(
        LoyaltyAccrualSource source,
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        long sourceMinorUnits,
        string currencyCode,
        string reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (sourceMinorUnits <= 0)
        {
            return null;
        }

        var settings = await dbContext.OrganizationLoyaltySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);
        if (settings is null)
        {
            return null;
        }

        var (enabled, basisPoints) = source switch
        {
            LoyaltyAccrualSource.TopUp => (settings.TopUpEnabled, settings.TopUpPercentBasisPoints),
            LoyaltyAccrualSource.Shop => (settings.ShopEnabled, settings.ShopPercentBasisPoints),
            LoyaltyAccrualSource.Session => (settings.SessionEnabled, settings.SessionPercentBasisPoints),
            _ => (false, 0)
        };
        if (!enabled || basisPoints <= 0)
        {
            return null;
        }

        // Minimum-to-qualify: the source amount must reach the org's threshold to earn anything.
        if (settings.MinimumSourceMinorUnits > 0 && sourceMinorUnits < settings.MinimumSourceMinorUnits)
        {
            return null;
        }

        var cashback = sourceMinorUnits * (long)basisPoints / 10000;
        if (cashback <= 0)
        {
            return null;
        }

        // Per-accrual cap: never grant more than the org's ceiling for a single event.
        if (settings.CashbackCapMinorUnits > 0 && cashback > settings.CashbackCapMinorUnits)
        {
            cashback = settings.CashbackCapMinorUnits;
        }

        // Cashback is a system-initiated grant (actor = Guid.Empty), not a cashier cash operation,
        // and its sources (online top-ups, shop deliveries) need not run within a shift — so the
        // entry is intentionally not tied to a shift (ShiftId stays null).
        return BillingEntryFactory.Create(
            organizationId,
            branchId,
            playerAccountId,
            sessionId,
            playerPackageId: null,
            LedgerEntryTypeNames.Cashback,
            LedgerAccountTypeNames.Wallet,
            cashback,
            quantitySeconds: 0,
            currencyCode,
            description: LedgerEntryTypeNames.Cashback,
            reason,
            reversesLedgerEntryId: null,
            actorStaffUserId: Guid.Empty,
            createdAtUtc);
    }
}
