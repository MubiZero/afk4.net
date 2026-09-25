namespace AFK4.Platform.Api.Data;

/// <summary>Чаевые с экрана ПК у клуба. Строки нет — выключены: это политика клуба, не платформы.</summary>
public sealed class OrganizationTipSettingsEntity
{
    public Guid OrganizationId { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid UpdatedByStaffUserId { get; set; }
}
