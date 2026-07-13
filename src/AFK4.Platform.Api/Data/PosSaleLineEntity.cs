namespace AFK4.Platform.Api.Data;

public sealed class PosSaleLineEntity
{
    public Guid PosSaleLineId { get; set; }

    public Guid PosSaleId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public long UnitPriceMinorUnits { get; set; }

    public long UnitCostMinorUnits { get; set; }

    public long LineTotalMinorUnits { get; set; }

    /// <summary>Immutable stock-tracking decision captured when the sale line is created.</summary>
    public bool TracksStock { get; set; }

    public bool AllowNegativeStock { get; set; }
}
