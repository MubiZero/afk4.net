namespace AFK4.Platform.Api.Data;

public sealed class PaymentEntity
{
    public Guid PaymentId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    // Exactly one of PosSaleId / SessionId is set: standalone POS payments carry
    // PosSaleId; unified session-checkout payments carry SessionId.
    public Guid? PosSaleId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public string PaymentKind { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public long AmountMinorUnits { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
