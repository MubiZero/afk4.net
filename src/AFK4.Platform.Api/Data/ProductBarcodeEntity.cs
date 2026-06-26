namespace AFK4.Platform.Api.Data;

public sealed class ProductBarcodeEntity
{
    public Guid BarcodeId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = "";
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
