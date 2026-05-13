namespace AFK4.Platform.Api.Data;

public sealed class CashMovementEntity
{
    public Guid CashMovementId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public long AmountMinorUnits { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
