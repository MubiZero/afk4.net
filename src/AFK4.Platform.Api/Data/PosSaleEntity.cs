namespace AFK4.Platform.Api.Data;

public sealed class PosSaleEntity
{
    public Guid PosSaleId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    // When set, this sale is rung up against an open session tab and stays unpaid
    // until the session is checked out (rather than being settled on its own).
    public Guid? SessionId { get; set; }

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
