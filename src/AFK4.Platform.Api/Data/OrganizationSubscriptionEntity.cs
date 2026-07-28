namespace AFK4.Platform.Api.Data;

public sealed class OrganizationSubscriptionEntity
{
    public Guid OrganizationSubscriptionId { get; set; }
    public Guid OrganizationId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = "trial";
    public DateTimeOffset CurrentPeriodStartUtc { get; set; }
    public DateTimeOffset CurrentPeriodEndUtc { get; set; }
    public DateTimeOffset? NextInvoiceUtc { get; set; }
    public long AmountMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public string BillingInterval { get; set; } = "monthly";
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
