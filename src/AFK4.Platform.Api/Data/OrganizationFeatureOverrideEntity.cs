namespace AFK4.Platform.Api.Data;

/// <summary>
/// Ручное исключение для конкретного клуба — верхняя ступень лестницы. Несёт причину и автора:
/// рубильник раскатки без объяснения через месяц никто не опознает.
/// </summary>
public sealed class OrganizationFeatureOverrideEntity
{
    public Guid OrganizationFeatureOverrideId { get; set; }

    public Guid OrganizationId { get; set; }

    public string FeatureKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid SetByPlatformAdminUserId { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }
}
