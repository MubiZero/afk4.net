namespace AFK4.Platform.Api.Data;

public sealed class StockMovementEntity
{
    public Guid StockMovementId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ProductId { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public int QuantityDelta { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public long UnitCostMinorUnits { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid CreatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
