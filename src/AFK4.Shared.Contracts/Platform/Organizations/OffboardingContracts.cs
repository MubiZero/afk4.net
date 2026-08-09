namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>Состояние ухода клуба: где он в цикле «заявка → выгрузка → стирание».</summary>
public sealed record OrganizationOffboardingDto(
    Guid OrganizationId,
    string Slug,
    string Status,
    DateTimeOffset? PurgeEligibleAtUtc,
    DateTimeOffset? PurgedAtUtc,
    bool CanPurge);

/// <summary>
/// Стирание подтверждается коротким именем клуба, набранным руками. Кнопка «да, я уверен» здесь
/// недостаточна: ошибиться клубом в списке легко, набрать чужой slug по памяти — нет.
/// </summary>
public sealed record PurgeOrganizationRequest(string Slug);

/// <summary>Сколько строк унесло стирание — по группам, для записи в аудит и показа человеку.</summary>
public sealed record PurgeOrganizationResultDto(
    int Players,
    int StaffUsers,
    int Sessions,
    int Sales,
    int Devices,
    int Branches,
    int ClubAuditRecords);

public static class OffboardingErrorCodes
{
    public const string NotDeletionPending = "not_deletion_pending";

    public const string PurgeNotDue = "purge_not_due";

    public const string SlugMismatch = "slug_mismatch";

    public const string OrganizationPurged = "organization_purged";
}
