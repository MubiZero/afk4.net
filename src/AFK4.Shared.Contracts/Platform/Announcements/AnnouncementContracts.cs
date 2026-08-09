namespace AFK4.Shared.Contracts.Platform.Announcements;

/// <summary>Анонс глазами платформы: что написано, кому, в каком он состоянии и дошёл ли.</summary>
public sealed record PlatformAnnouncementDto(
    Guid AnnouncementId,
    string Title,
    string Body,
    string Severity,
    DateTimeOffset ShowFromUtc,
    DateTimeOffset ShowUntilUtc,
    string AudienceKind,
    IReadOnlyList<string> AudiencePlanCodes,
    IReadOnlyList<Guid> AudienceOrganizationIds,
    string Status,
    DateTimeOffset? PublishedAtUtc,
    bool EmailDispatched,
    int ReadCount);

public sealed record SavePlatformAnnouncementRequest(
    string Title,
    string Body,
    string Severity,
    DateTimeOffset ShowFromUtc,
    DateTimeOffset ShowUntilUtc,
    string AudienceKind,
    IReadOnlyList<string> AudiencePlanCodes,
    IReadOnlyList<Guid> AudienceOrganizationIds);

/// <summary>Анонс глазами клуба: только то, что ему показывают, плюс прочитал ли ЭТОТ сотрудник.</summary>
public sealed record AnnouncementFeedItemDto(
    Guid AnnouncementId,
    string Title,
    string Body,
    string Severity,
    DateTimeOffset ShowFromUtc,
    DateTimeOffset ShowUntilUtc,
    DateTimeOffset PublishedAtUtc,
    bool IsRead);

/// <summary>
/// Машинные коды отказа. Прозу собирает клиент: конкретная причина, доехавшая до экрана, — это
/// разница между «поправь окно показа» и «что-то пошло не так».
/// </summary>
public static class AnnouncementErrorCodes
{
    public const string InvalidAnnouncement = "invalid_announcement";

    public const string UnknownPlan = "unknown_plan";

    public const string UnknownOrganization = "unknown_organization";

    public const string PublishedAudienceLocked = "published_audience_locked";

    public const string WithdrawnAnnouncement = "withdrawn_announcement";

    public const string InvalidTransition = "invalid_transition";

    public const string Conflict = "conflict";
}
