namespace AFK4.Platform.Api.Platform.Billing;

public sealed class BillingOptions
{
    public const string ConfigurationSection = "Billing";

    public TimeSpan InvoiceDueAfter { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan GenerationInterval { get; set; } = TimeSpan.FromHours(1);

    public string DefaultCurrencyCode { get; set; } = "TJS";

    /// <summary>How long before the due date the pre-due reminder goes out.</summary>
    public TimeSpan DueSoonReminderBefore { get; set; } = TimeSpan.FromDays(3);

    /// <summary>Overdue ladder rungs, as day offsets past the due date. Index + 1 is the stage number.</summary>
    public int[] DunningOffsetsAfterDue { get; set; } = [0, 3, 7, 14];
}
