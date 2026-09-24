namespace AFK4.Platform.Api.Data;

/// <summary>
/// Заявка «впусти этого человека на этот ПК», заведённая с телефона по QR (спека оболочки,
/// §5.4). Живёт минуту: ПК забирает её ключом устройства и получает токены, привязанные к себе.
/// </summary>
public sealed class PlayerSignInClaimEntity
{
    public Guid ClaimId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public Guid PlatformPersonId { get; set; }

    public Guid PlayerAccountId { get; set; }

    /// <summary>Ключ повтора с телефона: повтор после обрыва возвращает ту же заявку, а не вторую.</summary>
    public string IdempotencyKeyHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Когда ПК забрал заявку. Токен конкурентности: одна заявка — один вход.</summary>
    public DateTimeOffset? RedeemedAtUtc { get; set; }
}
