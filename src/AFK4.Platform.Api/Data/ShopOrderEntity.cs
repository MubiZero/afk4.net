namespace AFK4.Platform.Api.Data;

public sealed class ShopOrderEntity
{
    public Guid ShopOrderId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid SessionId { get; set; }

    public Guid SeatId { get; set; }

    public string Status { get; set; } = string.Empty;

    public long TotalMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    // The wallet debit entry; a cancellation writes a reversal that points back at it.
    public Guid WalletLedgerEntryId { get; set; }

    public Guid? PosSaleId { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string? CancelReason { get; set; }

    // Optimistic-concurrency token: bumped on every transition so two operators cannot double-act.
    public int Version { get; set; }
}
