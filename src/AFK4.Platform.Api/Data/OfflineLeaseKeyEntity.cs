namespace AFK4.Platform.Api.Data;

/// <summary>
/// Публичный ключ ПК администратора, которым Панель без интернета подписывает офлайн-аренды
/// (docs/roadmap/offline-front-desk.md). Приватная половина не покидает тот ПК. Отозванный ключ
/// уходит из списка, который агенты получают в сердцебиении.
/// </summary>
public sealed class OfflineLeaseKeyEntity
{
    public Guid OfflineLeaseKeyId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>Имя ключа в поле алгоритма аренды. Его придумывает Панель; уникально в филиале.</summary>
    public string KeyId { get; set; } = string.Empty;

    public string PublicKeyPem { get; set; } = string.Empty;

    public Guid RegisteredByStaffUserId { get; set; }

    public DateTimeOffset RegisteredAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
