namespace AFK4.Platform.Api.Outbox;

/// <summary>Configuration for the billing outbox dispatcher, bound from the <c>Outbox</c> section.</summary>
public sealed class OutboxOptions
{
    public const string ConfigurationSection = "Outbox";

    /// <summary>How often the dispatcher polls for due rows.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum rows the dispatcher claims per tick.</summary>
    public int DispatchBatchSize { get; set; } = 50;

    /// <summary>Total dispatch attempts before a row is marked Failed.</summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>Capped exponential backoff ladder; the last entry repeats once exhausted.</summary>
    public IReadOnlyList<TimeSpan> BackoffSchedule { get; set; } =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
    ];
}
