namespace AFK4.Platform.Api.Platform.Health;

public sealed class PlatformHealthOptions
{
    public const string ConfigurationSection = "Health";

    public TimeSpan WatchInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Сообщение очереди, ждущее дольше этого срока, считается застрявшим.</summary>
    public TimeSpan QueueStuckThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Прогоны старше этого срока удаляются — история outbox за год не нужна никому.</summary>
    public TimeSpan JobRunRetention { get; set; } = TimeSpan.FromDays(30);
}
