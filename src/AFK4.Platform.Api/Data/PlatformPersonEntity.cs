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

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
