namespace AFK4.Platform.Api.Data;

/// <summary>
/// A recipient's opt-out for one (category, channel). Only Operational/Digest categories are ever
/// stored here — Transactional always sends and ignores preferences (D4). Absence of a row means
/// default-on, so silence is a deliberate choice rather than a missing record.
/// </summary>
public sealed class NotificationPreferenceEntity
{
    public Guid NotificationPreferenceId { get; set; }

    public Guid? StaffUserId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public bool OptedOut { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}
