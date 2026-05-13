namespace AFK4.Platform.Api.Data;

public sealed class ReceiptEntity
{
    public Guid ReceiptId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PosSaleId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public string ReceiptType { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public long TotalMinorUnits { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
