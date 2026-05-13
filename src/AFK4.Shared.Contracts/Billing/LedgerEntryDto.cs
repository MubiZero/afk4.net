namespace AFK4.Shared.Contracts.Billing;

public sealed record LedgerEntryDto(
    Guid LedgerEntryId,
    Guid OrganizationId,
    Guid BranchId,
    Guid PlayerAccountId,
    Guid? SessionId,
    Guid? PlayerPackageId,
    string EntryType,
    string AccountType,
    MoneyDto Amount,
    int QuantitySeconds,
    string Description,
    string Reason,
    Guid? ReversesLedgerEntryId,
    Guid CreatedByStaffUserId,
    DateTimeOffset CreatedAtUtc);
