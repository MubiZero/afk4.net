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

    // --- SMTP / email channel (D8) ---

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string? FromName { get; set; }

    /// <summary>Cash variance (absolute minor units) a closed shift may have before the owner is alerted. Default 0 = alert on any discrepancy.</summary>
    public long ShiftDiscrepancyToleranceMinorUnits { get; set; }

    /// <summary>How often the daily owner-summary service ticks. Idempotency per (org, date) makes repeated ticks safe, so a sub-day interval just means the prior day's summary goes out promptly after midnight UTC.</summary>
    public TimeSpan DailySummaryInterval { get; set; } = TimeSpan.FromHours(1);

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
