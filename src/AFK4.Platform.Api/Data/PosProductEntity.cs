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

    /// <summary>Средневзвешенная закупочная себестоимость единицы (minor units). Пересчитывается при purchase-движении.</summary>
    public long AvgCostMinorUnits { get; set; }

    public bool TrackStock { get; set; }

    public bool AllowNegativeStock { get; set; }

    /// <summary>Stock-on-hand at or below this (when &gt; 0 and tracked) alerts the owner. 0 = no low-stock alerting.</summary>
    public int ReorderThreshold { get; set; }

    /// <summary>True while a low-stock alert is outstanding; re-armed (false) when stock returns above the threshold.</summary>
    public bool LowStockAlerted { get; set; }

    /// <summary>Restock-cycle counter; bumped on each re-arm so each cycle's alert carries a distinct idempotency key.</summary>
    public int LowStockCycle { get; set; }

    public bool IsActive { get; set; }

    /// <summary>True when this product is offered to players in the shell shop (delivery to seat).</summary>
    public bool AvailableInShell { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
