namespace AFK4.Platform.Api.Data;

public sealed class InvoiceEntity
{
    public Guid InvoiceId { get; set; }
    public Guid OrganizationId { get; set; }
    public int Number { get; set; }
    public string Kind { get; set; } = "subscription";
    public DateTimeOffset PeriodStartUtc { get; set; }
    public DateTimeOffset PeriodEndUtc { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public long AmountMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = "TJS";
    public string Status { get; set; } = "issued";
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>0 = nothing sent; 1..4 are the overdue ladder rungs (see the wave B design spec).</summary>
    public int DunningStage { get; set; }

    public DateTimeOffset? LastDunningAtUtc { get; set; }

    public DateTimeOffset? DueSoonNotifiedAtUtc { get; set; }

    /// <summary>Amount before the subscription discount; equals AmountMinorUnits when there is none.
    /// The discount itself arrives in task 6 — the columns land here so every later task can seed
    /// invoices without a second migration.</summary>
    public long GrossAmountMinorUnits { get; set; }

    public long DiscountMinorUnits { get; set; }
}
