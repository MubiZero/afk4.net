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
    public string CurrencyCode { get; set; } = "TJS";
    public string BillingInterval { get; set; } = "monthly";
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? PaymentGraceUntilUtc { get; set; }
    public int? DiscountPercent { get; set; }
    public long? DiscountAmountMinorUnits { get; set; }
    public DateTimeOffset? DiscountUntilUtc { get; set; }
    public string? DiscountReason { get; set; }
    /// <summary>Клуб сам начал пробный период — второй раз нельзя.</summary>
    public DateTimeOffset? TrialStartedAtUtc { get; set; }
    /// <summary>Счёт, под который клуб взял обещанный платёж: один раз на счёт.</summary>
    public Guid? PromisedPaymentInvoiceId { get; set; }
    /// <summary>Бесплатные месяцы за приведённые клубы: каждый обнуляет один счёт за подписку.</summary>
    public int FreeMonths { get; set; }
}
