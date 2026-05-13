namespace AFK4.Platform.Api.Data;

public sealed class LedgerEntryEntity
{
    public Guid LedgerEntryId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? ShiftId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? PlayerPackageId { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public string AccountType { get; set; } = string.Empty;

    public long AmountMinorUnits { get; set; }

    public int QuantitySeconds { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public string Description { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public Guid? ReversesLedgerEntryId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
