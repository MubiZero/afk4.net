using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// Что можно купить, сев за этот ПК, — одним запросом, с готовыми суммами (спека оболочки,
/// §5.5). Клиент цену не считает: суммы считает тот же расчёт, что и списание, иначе экран
/// однажды пообещал бы одну цифру, а касса списала бы другую.
/// </summary>
public sealed record PlayerStartOffersDto(
    string? SeatLabel,
    string? ZoneName,
    // Часовой пояс клуба (IANA): «до скольки» показывается по времени клуба, а не телефона.
    string TimeZone,
    MoneyDto Balance,
    IReadOnlyList<PlayerTariffOfferDto> Tariffs,
    IReadOnlyList<PlayerPackageOfferDto> Packages);

public sealed record PlayerTariffOfferDto(
    Guid TariffVersionId,
    // То, что передаётся в старт как TariffRuleVersionId.
    string TariffRuleVersionId,
    string Name,
    MoneyDto PricePerHour,
    bool AppliesNow,
    // Когда тариф откроется, если сейчас он не действует; вариантов у такого тарифа нет.
    DateTimeOffset? StartsAtUtc,
    IReadOnlyList<PlayerDurationOfferDto> Options);

public sealed record PlayerDurationOfferDto(
    // Сколько времени берёт игрок.
    int Minutes,
    // Сколько минут будет оплачено: минимум и шаг округления тарифа уже применены.
    int BillableMinutes,
    DateTimeOffset EndsAtUtc,
    MoneyDto Amount,
    MoneyDto BalanceAfter,
    bool Affordable);

public sealed record PlayerPackageOfferDto(
    Guid PlayerPackageId,
    string Name,
    int RemainingMinutes,
    DateTimeOffset? ExpiresAtUtc);

/// <summary>Чем можно продлить идущую сессию — по тарифу, на котором она началась.</summary>
public sealed record PlayerExtendOffersDto(
    Guid SessionId,
    MoneyDto Balance,
    IReadOnlyList<PlayerDurationOfferDto> Options,
    // Одно из PlayerOfferUnavailableReasonNames; пусто, если продлить можно.
    string? UnavailableReason = null);

public static class PlayerOfferUnavailableReasonNames
{
    /// <summary>Сессия начата по пакету: её продлевают новым стартом по пакету, а не деньгами.</summary>
    public const string PackageSession = "package_session";

    /// <summary>Сессия не предоплаченная — у стойки или открытым счётом; продлевает администратор.</summary>
    public const string NotPrepaid = "not_prepaid";
}
