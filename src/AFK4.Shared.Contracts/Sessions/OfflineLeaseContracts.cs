namespace AFK4.Shared.Contracts.Sessions;

/// <summary>Публичный ключ ПК администратора, которым тот подписывает офлайн-аренды.</summary>
public sealed record OfflineLeaseKeyDto(string KeyId, string PublicKeyPem);

/// <summary>Что ПК ответил Панели на офлайн-аренду, присланную по сети клуба.</summary>
public sealed record OfflineSeatResponse(
    bool Accepted,
    string? Reason,
    bool IsLocked,
    Guid? ActiveSessionId,
    DateTimeOffset? LeaseExpiresAtUtc);

/// <summary>
/// Офлайн-аренда — обычная <see cref="SessionLeaseDto"/>, подписанная не платформой, а ключом ПК
/// администратора (docs/roadmap/offline-front-desk.md). Ключ называется в поле алгоритма, а оно
/// входит в подписанную запись: подменить ключ, не сломав подпись, нельзя.
/// </summary>
public static class OfflineLeaseSigning
{
    public const string AlgorithmPrefix = "ECDSA-P256-SHA256+offline:";

    /// <summary>Порт агента в сети клуба и путь, по которому Панель передаёт офлайн-аренду.</summary>
    public const int AgentLanPort = 47615;

    public const string AgentLeasePath = "/afk4/offline/lease";

    /// <summary>
    /// Дольше офлайн-аренда не бывает: решение владельца от 30.09 — без связи ПК держится до конца
    /// оплаты, но не больше четырёх часов.
    /// </summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(4);

    /// <summary>Состояние аренды, которое запирает ПК: «сессия окончена».</summary>
    public const string EndedState = "Ended";

    public static string Algorithm(string keyId) => AlgorithmPrefix + keyId;

    public static bool TryGetKeyId(string signatureAlgorithm, out string keyId)
    {
        keyId = string.Empty;
        if (!signatureAlgorithm.StartsWith(AlgorithmPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        keyId = signatureAlgorithm[AlgorithmPrefix.Length..];
        return keyId.Length > 0;
    }
}

/// <summary>Панель регистрирует ключ своего ПК, пока связь есть: потом сервер раздаст его агентам филиала.</summary>
public sealed record RegisterOfflineLeaseKeyRequest(string KeyId, string PublicKeyPem);

public sealed record BranchOfflineLeaseKeyDto(
    string KeyId,
    Guid RegisteredByStaffUserId,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? RevokedAtUtc);

public static class OfflineLeaseKeyErrorCodeNames
{
    /// <summary>Имя ключа занято другим ключом в этом филиале.</summary>
    public const string Conflict = "offline_key_conflict";

    /// <summary>Ключ с этим именем отозван — Панель создаёт новый.</summary>
    public const string Revoked = "offline_key_revoked";

    /// <summary>Не ключ P-256 в PEM или имя не по правилам.</summary>
    public const string Invalid = "offline_key_invalid";

    /// <summary>Действующих ключей в филиале слишком много: старые надо отозвать.</summary>
    public const string Limit = "offline_key_limit";
}
