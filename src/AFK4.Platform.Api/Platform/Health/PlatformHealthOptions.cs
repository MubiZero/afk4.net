namespace AFK4.Platform.Api.Platform.Health;

public sealed class PlatformHealthOptions
{
    public const string ConfigurationSection = "Health";

    public TimeSpan WatchInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Сообщение очереди, ждущее дольше этого срока, считается застрявшим.</summary>
    public TimeSpan QueueStuckThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// За какой срок провалы считаются свежими. `Failed` — терминальный статус: строка с ним лежит
    /// в таблице вечно, поэтому счёт «за всю историю» держал бы инцидент открытым навсегда, и
    /// постоянно горящий красный индикатор перестают читать.
    /// </summary>
    public TimeSpan QueueFailureWindow { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Прогоны старше этого срока удаляются — история outbox за год не нужна никому.</summary>
    public TimeSpan JobRunRetention { get; set; } = TimeSpan.FromDays(30);
}
