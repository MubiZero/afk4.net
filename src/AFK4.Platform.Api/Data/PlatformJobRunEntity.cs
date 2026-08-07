namespace AFK4.Platform.Api.Data;

/// <summary>Один прогон периодического задания: чем кончился и сколько обработал.</summary>
public sealed class PlatformJobRunEntity
{
    public Guid PlatformJobRunId { get; set; }

    public string JobName { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset FinishedAtUtc { get; set; }

    public string Outcome { get; set; } = PlatformJobOutcomeNames.Succeeded;

    public int ItemsProcessed { get; set; }

    /// <summary>Усечённый текст ошибки; null при успехе.</summary>
    public string? Error { get; set; }
}

public static class PlatformJobOutcomeNames
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}
