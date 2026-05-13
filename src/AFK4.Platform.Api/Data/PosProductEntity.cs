namespace AFK4.Platform.Api.Data;

public sealed class PosProductEntity
{
    public Guid ProductId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public long PriceMinorUnits { get; set; }

    public bool TrackStock { get; set; }

    public bool AllowNegativeStock { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
