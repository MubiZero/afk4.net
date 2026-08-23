namespace AFK4.Platform.Api.Data;

/// <summary>
/// Единственный способ завести сотрудника клуба: владелец приглашает по номеру телефона, человек
/// принимает приглашение коротким кодом из SMS и задаёт себе пароль сам.
///
/// Номер, а не почта: у администратора зала почты может не быть вовсе, а телефон есть наверняка —
/// и он же служит входом (см. <c>NormalizedPhone</c> в <see cref="StaffUserEntity"/>). Код
/// шестизначный и хранится хешем; живёт сутки и умирает после трёх неверных попыток.
/// </summary>
public sealed class StaffInviteEntity
{
    public Guid StaffInviteId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Номер, на который ушёл код: «+&lt;цифры&gt;», как у сотрудников и игроков.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Тот же номер только цифрами — по нему приглашение и находят при приёме.</summary>
    public string NormalizedPhone { get; set; } = string.Empty;

    /// <summary>Почта необязательна: если её назвали, приглашение уходит и письмом тоже.</summary>
    public string? Email { get; set; }

    /// <summary>Comma-separated branch role names to assign on accept.</summary>
    public string RoleNamesCsv { get; set; } = string.Empty;

    /// <summary>SHA-256 от шестизначного кода. Открытым текстом код живёт только в SMS.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Сколько раз ошиблись кодом. Три — и приглашение мертво.</summary>
    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? AcceptedByStaffUserId { get; set; }
}
