namespace AFK4.Platform.Api.Data;

/// <summary>
/// Кто кого привёл.
///
/// Отдельная таблица, а не пара колонок у игрока: приглашение — это связь двоих со своей
/// историей (когда назвали код, когда заплатили), и у приглашённого она ровно одна. Отсюда и
/// первичный ключ по приглашённому: второй код тем же человеком не назовёшь.
/// </summary>
public sealed class PlayerReferralEntity
{
    /// <summary>Приглашённый. Он же ключ: код называют один раз в жизни.</summary>
    public Guid InviteePlayerAccountId { get; set; }

    public Guid ReferrerPlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public DateTimeOffset ClaimedAtUtc { get; set; }

    /// <summary>Когда заплатили обоим. null — друг ещё не дошёл до первого пополнения.</summary>
    public DateTimeOffset? RewardedAtUtc { get; set; }

    public long ReferrerBonusMinorUnits { get; set; }

    public long InviteeBonusMinorUnits { get; set; }

    public string? CurrencyCode { get; set; }
}
