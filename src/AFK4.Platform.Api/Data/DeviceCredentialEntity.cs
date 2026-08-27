namespace AFK4.Platform.Api.Data;

public sealed class DeviceCredentialEntity
{
    public Guid CredentialId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>
    /// Докуда старый ключ ещё принимается после того, как агент перевыпустил себе новый.
    /// null — ключ бессрочный (обычное состояние живого ключа).
    ///
    /// Нужен ради одного окна: между «сервер выдал новый ключ» и «агент успел записать его на
    /// диск» ПК может выключиться. Без перекрытия такая секунда оставляла бы машину без входа
    /// до визита человека — то есть ровно то, ради чего эта работа и делается.
    /// Отзыв (<see cref="RevokedAtUtc"/>) перекрытия не даёт: он на то и отзыв.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
