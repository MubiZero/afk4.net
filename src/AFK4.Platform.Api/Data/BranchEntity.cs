namespace AFK4.Platform.Api.Data;

public sealed class BranchEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public bool RequireManualDeviceApproval { get; set; }

    /// <summary>
    /// BCP-47-ish locale ("ru" | "en" | "tg") that drives the customer-facing shell language
    /// for this branch and the default locale for its receipts and notifications.
    /// </summary>
    public string PreferredLocale { get; set; } = "ru";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
