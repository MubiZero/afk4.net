namespace AFK4.Platform.Api.Data;

// Конфиг приёма DushanbeCity: карта приёма (шифрованный PAN) + шаблон комментария. Org-уровень
// (BranchId=null) в v1, как EskhataMerchantConfig. Отдельно от удалённого dcgate — тут нет
// API-проекта/Telegram: DC-ссылка «тупая», подтверждает кассир.
public sealed class DcPayLinkConfigEntity
{
    public Guid DcPayLinkConfigId { get; set; }

    public Guid OrganizationId { get; set; }

    // null => org-уровень (v1 использует только его).
    public Guid? BranchId { get; set; }

    // Полный номер карты приёма, шифрован ISecretProtector. Нужен для сборки ссылки.
    public string ReceivingCardEncrypted { get; set; } = string.Empty;

    // Последние 4 цифры для показа в UI (наружу PAN не отдаём).
    public string CardLast4 { get; set; } = string.Empty;

    // Шаблон комментария платежа, {ref} заменяется на короткий id намерения.
    public string CommentTemplate { get; set; } = "AFK4-{ref}";

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
