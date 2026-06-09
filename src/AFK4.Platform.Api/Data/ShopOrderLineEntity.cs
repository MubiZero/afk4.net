namespace AFK4.Platform.Api.Data;

public sealed class ShopOrderLineEntity
{
    public Guid ShopOrderLineId { get; set; }
    public Guid ShopOrderId { get; set; }
    public Guid ProductId { get; set; }
    public string NameSnapshot { get; set; } = string.Empty;
    public long UnitPriceMinorUnits { get; set; }
    public int Quantity { get; set; }
    public long LineTotalMinorUnits { get; set; }
}
