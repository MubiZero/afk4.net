namespace AFK4.Platform.Api.Data;

/// <summary>
/// «Приведи друга», как его настроил клуб.
///
/// Платит клуб, а не платформа, и суммы задаёт он же — тем же правилом, что и кешбэк. Выключено
/// по умолчанию: программа лояльности, включённая без ведома владельца, начинает раздавать его
/// деньги.
/// </summary>
public sealed class OrganizationReferralSettingsEntity
{
    public Guid OrganizationId { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Сколько получает пригласивший, когда друг доходит до первого пополнения.</summary>
    public long ReferrerBonusMinorUnits { get; set; }

    /// <summary>Сколько получает сам приглашённый.</summary>
    public long InviteeBonusMinorUnits { get; set; }

    /// <summary>
    /// Пополнение меньше этой суммы бонус не запускает. Иначе привести друга и положить ему
    /// один дирам — готовый способ печатать деньги клуба.
    /// </summary>
    public long MinimumTopUpMinorUnits { get; set; }

    /// <summary>
    /// Сколько дней после заведения аккаунта друг может назвать код. Приглашение — про новых
    /// игроков; без окна давний завсегдатай однажды введёт код и обналичит дружбу задним числом.
    /// Ноль — окна нет.
    /// </summary>
    public int ClaimWindowDays { get; set; }

    /// <summary>Сколько друзей одного игрока оплачивается. Ноль — без ограничения.</summary>
    public int MaxRewardedPerReferrer { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
