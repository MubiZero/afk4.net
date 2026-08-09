namespace AFK4.Platform.Api.Data;

/// <summary>
/// Одно сообщение платформы клубам. Тело пишет живой человек и хранится одной строкой на одном
/// языке: три поля означали бы, что кто-то переводит каждый анонс на таджикский, а в жизни туда
/// скопировали бы русский. Интерфейс вокруг локализуется как обычно.
/// </summary>
public sealed class PlatformAnnouncementEntity
{
    public Guid PlatformAnnouncementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Severity { get; set; } = AnnouncementSeverity.Info;

    public DateTimeOffset ShowFromUtc { get; set; }

    public DateTimeOffset ShowUntilUtc { get; set; }

    public string AudienceKind { get; set; } = AnnouncementAudienceKinds.All;

    public string AudiencePlanCodesJson { get; set; } = "[]";

    public string AudienceOrganizationIdsJson { get; set; } = "[]";

    public string Status { get; set; } = AnnouncementStatus.Draft;

    public Guid CreatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? PublishedAtUtc { get; set; }

    /// <summary>
    /// Момент, когда письма по этому анонсу были поставлены в очередь. Ставится ровно один раз:
    /// снять анонс и опубликовать заново — это то же сообщение, а не второе письмо владельцу.
    /// </summary>
    public DateTimeOffset? EmailDispatchedAtUtc { get; set; }
}

/// <summary>Отметка «прочитано» — на сотрудника, а не на клуб.</summary>
public sealed class AnnouncementReadEntity
{
    public Guid PlatformAnnouncementId { get; set; }

    public Guid StaffUserId { get; set; }

    public DateTimeOffset ReadAtUtc { get; set; }
}

public static class AnnouncementSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";

    public static readonly IReadOnlyList<string> All = [Info, Warning, Critical];

    /// <summary>Письмо владельцу уходит от «предупреждения» и выше: «к сведению» письмом не шлют.</summary>
    public static bool WarrantsEmail(string severity) =>
        severity is Warning or Critical;

    /// <summary>Чем больше, тем важнее: полоса показывает самый важный непрочитанный анонс.</summary>
    public static int Rank(string severity) => severity switch
    {
        Critical => 2,
        Warning => 1,
        _ => 0
    };
}

public static class AnnouncementAudienceKinds
{
    public const string All = "all";
    public const string Plans = "plans";
    public const string Organizations = "organizations";

    public static readonly IReadOnlyList<string> AllKinds = [All, Plans, Organizations];
}

public static class AnnouncementStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Withdrawn = "withdrawn";
}
