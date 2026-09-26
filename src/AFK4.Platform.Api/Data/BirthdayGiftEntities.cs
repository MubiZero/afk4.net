namespace AFK4.Platform.Api.Data;

/// <summary>
/// Подарок на день рождения, как его настроил клуб (владелец, 2026-09-26: «акции на др»). Платит
/// клуб — тем же правилом, что кешбэк и «приведи друга»; выключено по умолчанию.
/// </summary>
public sealed class OrganizationBirthdayGiftSettingsEntity
{
    public Guid OrganizationId { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Сколько ложится на баланс в день рождения.</summary>
    public long AmountMinorUnits { get; set; }

    /// <summary>
    /// Подарок только тем, кто был в клубе за столько дней. Иначе клуб дарил бы деньги каждому, кто
    /// однажды зашёл три года назад. Ноль — всем.
    /// </summary>
    public int RecentVisitDays { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Подарок, который уже вручили: один на счёт игрока в год. Отметка ставится вместе с проводкой —
/// у журнала денег нет своего ключа от повтора, и без отметки второй прогон задания подарил бы ещё раз.
/// </summary>
public sealed class PlayerBirthdayGiftEntity
{
    public Guid PlayerBirthdayGiftId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public int Year { get; set; }

    public long AmountMinorUnits { get; set; }

    public Guid LedgerEntryId { get; set; }

    public DateTimeOffset GrantedAtUtc { get; set; }
}
