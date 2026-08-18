namespace AFK4.Platform.Api.Data;

/// <summary>
/// Спорный случай, найденный при переносе клубных карточек на личности. Перенос ничего не
/// блокирует и ничего не решает за человека: он записывает находку и идёт дальше, а разбирает её
/// потом живой человек через Platform Control.
/// </summary>
public sealed class PlatformIdentityMigrationFindingEntity
{
    public Guid FindingId { get; set; }

    /// <summary>Вид находки: см. <see cref="PlatformIdentityMigrationFindingKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid? PlatformPersonId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public Guid? OrganizationId { get; set; }

    public string DetailsJson { get; set; } = "{}";

    /// <summary>
    /// Когда находка появилась. Перенос наполняет таблицу разом, но разбирать её будут месяцами, и
    /// без даты в очереди разбора нет ни порядка, ни ответа на вопрос «это старое или новое».
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public static class PlatformIdentityMigrationFindingKinds
{
    /// <summary>Один номер в одном клубе дважды: к личности подшит один счёт, второй остался клубным.</summary>
    public const string DuplicateInClub = "duplicate_in_club";

    /// <summary>Один номер, разные имена в разных клубах — кандидат в «один номер, два человека».</summary>
    public const string NameMismatch = "name_mismatch";

    /// <summary>Номер, который не разбирается в международный: счёт остался чисто клубным.</summary>
    public const string UnusablePhone = "unusable_phone";
}
