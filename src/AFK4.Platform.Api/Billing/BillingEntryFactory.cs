using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Billing;

public static class BillingEntryFactory
{
    public static LedgerEntryEntity Create(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        Guid? playerPackageId,
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        string currencyCode,
        string description,
        string reason,
        Guid? reversesLedgerEntryId,
        Guid actorStaffUserId,
        DateTimeOffset createdAtUtc,
        Guid? shiftId = null)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ShiftId = shiftId,
            PlayerAccountId = playerAccountId,
            SessionId = sessionId,
            PlayerPackageId = playerPackageId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = currencyCode,
            Description = description,
            Reason = reason,
            ReversesLedgerEntryId = reversesLedgerEntryId,
            CreatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = createdAtUtc
        };
    }
}
