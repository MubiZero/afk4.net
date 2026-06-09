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
            _ => (false, 0)
        };
        if (!enabled || basisPoints <= 0)
        {
            return null;
        }

        var cashback = sourceMinorUnits * (long)basisPoints / 10000;
        if (cashback <= 0)
        {
            return null;
        }

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
