namespace AFK4.Platform.Api.Data;

/// <summary>
/// Человек, а не клиент клуба. Личность живёт на платформе целиком: телефон, имя, язык, сетевой
/// PIN и сетевой запрет принадлежат ей. Деньги, кешбэк, стаж и долг остаются клубными и живут в
/// <see cref="PlayerAccountEntity"/> — у каждого клуба своя касса, общего кошелька нет.
/// </summary>
public sealed class PlatformPersonEntity
{
    public Guid PlatformPersonId { get; set; }

    /// <summary>Канонический номер в форме «+&lt;11–15 цифр&gt;» — та же форма, что в player_accounts.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Язык общения; null означает «взять язык филиала или язык по умолчанию».</summary>
    public string? PreferredLocale { get; set; }

    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }

    /// <summary>
    /// День рождения — по желанию человека (владелец, 2026-09-26): для подарка клуба и для игр
    /// с возрастом. Живёт у личности, а не у клуба: человек вводит его один раз для всей сети.
    /// </summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>
    /// Когда дату ввели или сменили. Подарок на день рождения требует даты, введённой заранее, —
    /// иначе её подкручивали бы под подарок.
    /// </summary>
    public DateTimeOffset? BirthDateSetAtUtc { get; set; }

    /// <summary>
    /// Сетевой PIN: короткий числовой пароль для самопосадки за ПК в любом клубе сети. Null у всех,
    /// кто ещё не задал его сам — клубные PIN сюда не переносятся никогда, иначе админ одного клуба
    /// получил бы вход от чужого имени в чужих клубах.
    /// </summary>
    public string? PinHash { get; set; }

    public DateTimeOffset? PinSetAtUtc { get; set; }

    public int PinFailedCount { get; set; }

    public DateTimeOffset? PinLockedUntilUtc { get; set; }

    /// <summary>Запрет по всей сети. Локальные клубные запреты остаются клубными и сюда не попадают.</summary>
    public DateTimeOffset? NetworkBanAtUtc { get; set; }

    public string? NetworkBanReason { get; set; }

    /// <summary>
    /// Видят ли друзья, что человек сейчас в зале. По умолчанию да: друга человек принял сам,
    /// а список друзей, в котором никто никогда не «в зале», — это список без смысла. Выключается
    /// одним переключателем и действует сразу на всех.
    /// </summary>
    public bool ShowsPresenceToFriends { get; set; } = true;

    /// <summary>
    /// Докуда человек прочитал свои уведомления. Отметка одна на всё, а не строка на каждое
    /// сообщение: список уведомлений читают целиком, открыв его, и отдельной таблицы «прочитано»
    /// ради этого заводить незачем. Null — не открывал ни разу.
    /// </summary>
    public DateTimeOffset? NotificationsReadAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
