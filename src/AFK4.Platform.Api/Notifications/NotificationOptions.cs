namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Configuration for the notification backbone, bound from the <c>Notifications</c> section.
/// SMTP/channel fields are added by the email channel slice; this carries the cross-cutting
/// settings the service and dispatcher need.
/// </summary>
public sealed class NotificationOptions
{
    public const string ConfigurationSection = "Notifications";

    /// <summary>Locale used when a recipient has none / an unknown one (D12).</summary>
    public string DefaultLocale { get; set; } = "ru";

    /// <summary>How often the dispatcher polls for due rows.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Maximum rows the dispatcher claims per tick.</summary>
    public int DispatchBatchSize { get; set; } = 50;

    /// <summary>Total delivery attempts before a row is marked Failed (D11).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Capped exponential backoff ladder; the last entry repeats once exhausted (D11).</summary>
    public IReadOnlyList<TimeSpan> BackoffSchedule { get; set; } =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
    ];
}
