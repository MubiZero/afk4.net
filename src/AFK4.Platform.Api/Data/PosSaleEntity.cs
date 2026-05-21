namespace AFK4.Platform.Api.Data;

public sealed class PosSaleEntity
{
    public Guid PosSaleId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public string State { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public long TotalMinorUnits { get; set; }

    public string RefundReason { get; set; } = string.Empty;

    public string VoidReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? PaidAtUtc { get; set; }

    public DateTimeOffset? RefundedAtUtc { get; set; }

    public DateTimeOffset? VoidedAtUtc { get; set; }
}
