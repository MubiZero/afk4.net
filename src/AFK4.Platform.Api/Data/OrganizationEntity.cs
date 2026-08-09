namespace AFK4.Platform.Api.Data;

public sealed class OrganizationEntity
{
    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? LegalDetails { get; set; }

    public string? LogoUrl { get; set; }

    public string? AccentColor { get; set; }

    public string Status { get; set; } = "active";

    public string? StatusReason { get; set; }

    public DateTimeOffset? StatusChangedAtUtc { get; set; }

    public string PlanCode { get; set; } = "starter";

    public string SubscriptionStatus { get; set; } = "trial";

    public string LimitsJson { get; set; } = "{}";

    /// <summary>
    /// Раньше этой даты стирать нельзя. Ставится при переводе в «уходит», по умолчанию через
    /// месяц: всё это время данные целы и клуб возвращается в работу сменой статуса. Передумать —
    /// нормальный сценарий, а не исключение.
    /// </summary>
    public DateTimeOffset? PurgeEligibleAtUtc { get; set; }

    /// <summary>Когда клуб стёрли. Вместе со статусом отличает «ушёл и стёрт» от «собирается уйти».</summary>
    public DateTimeOffset? PurgedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Update channel this organization's clients pin to (see <see cref="AFK4.Shared.Contracts.Updates.UpdateChannelNames"/>).</summary>
    public string UpdateChannel { get; set; } = "stable";

    /// <summary>Optional client version this organization is pinned to; null = follow the channel's latest.</summary>
    public string? PinnedClientVersion { get; set; }
}
