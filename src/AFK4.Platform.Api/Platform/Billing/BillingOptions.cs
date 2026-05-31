namespace AFK4.Platform.Api.Platform.Billing;

public sealed class BillingOptions
{
    public const string ConfigurationSection = "Billing";

    public TimeSpan InvoiceDueAfter { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan GenerationInterval { get; set; } = TimeSpan.FromHours(1);

    public string DefaultCurrencyCode { get; set; } = "RUB";
}
