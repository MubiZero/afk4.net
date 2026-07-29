namespace AFK4.Platform.Api.Data;

public sealed class UpdateRolloutEntity
{
    public Guid UpdateRolloutId { get; set; }

    public Guid UpdatePackageId { get; set; }

    public string Component { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string TargetKind { get; set; } = string.Empty;

    public int BatchPercent { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid CreatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
