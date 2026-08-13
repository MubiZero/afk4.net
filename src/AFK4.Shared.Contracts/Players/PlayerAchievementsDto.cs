namespace AFK4.Shared.Contracts.Players;

/// Игровой стаж как его видит игрок: уровень, часы за ПК и список достижений.
///
/// Названия достижений сюда не попадают — только коды: подписи живут в приложении, где у них
/// есть три языка. Сервер, который присылал бы «Ночной житель» строкой, говорил бы с игроком
/// на языке базы данных.
public sealed record PlayerAchievementsDto(
    int Level,
    int VisitCount,
    long PlayedMinutes,
    /// Сколько минут до следующего уровня; null — уровень последний.
    long? MinutesToNextLevel,
    IReadOnlyList<PlayerAchievementDto> Achievements);

/// Одно достижение: код, порог и насколько игрок к нему подошёл. Прогресс виден и до
/// получения — иначе список выглядит как набор запертых дверей без замочных скважин.
public sealed record PlayerAchievementDto(
    string Code,
    int Progress,
    int Target,
    DateTimeOffset? UnlockedAtUtc);

public static class PlayerAchievementCodes
{
    public const string FirstVisit = "first_visit";
    public const string Regular = "regular";
    public const string Veteran = "veteran";
    public const string NightOwl = "night_owl";
    public const string Marathon = "marathon";
    public const string Reviewer = "reviewer";
}
